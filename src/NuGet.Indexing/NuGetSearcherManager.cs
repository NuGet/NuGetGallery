// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class NuGetSearcherManager : SearcherManager<NuGetIndexSearcher>
    {
        private readonly ILoader _loader;
        private readonly FrameworkLogger _logger;

        public NuGetSearcherManager(string indexName,
            FrameworkLogger logger,
            Lucene.Net.Store.Directory directory,
            ILoader loader)
            : base(directory)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _logger = logger;

            IndexName = indexName;
            RegistrationBaseAddress = new Dictionary<string, Uri>();

            _loader = loader;
        }

        public string IndexName { get; private set; }
        public IDictionary<string, Uri> RegistrationBaseAddress { get; }

        /// <summary>Initializes a <see cref="NuGetSearcherManager"/> instance.</summary>
        /// <param name="configuration">
        /// The configuration to read which primarily determines whether the resulting instance will read from the local
        /// disk or from blob storage.
        /// </param>
        /// <param name="directory">
        /// Optionally, the Lucene directory to read the index from. If <c>null</c> is provided, the directory
        /// implementation is determined based off of the configuration (<see cref="configuration"/>).
        /// </param>
        /// <param name="loader">
        /// Optionally, the loader used to read the JSON data files. If <c>null</c> is provided, the loader
        /// implementation is determined based off of the configuration (<see cref="configuration"/>).
        /// </param>
        /// <param name="loggerFactory">
        /// Optionally, the logger factory defined by the consuming application.
        /// </param>
        /// <returns>The resulting <see cref="NuGetSearcherManager"/> instance.</returns>
        public static NuGetSearcherManager Create(IConfiguration configuration,
            ILoggerFactory loggerFactory,
            Lucene.Net.Store.Directory directory = null,
            ILoader loader = null)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            LoggerFactory = loggerFactory;

            NuGetSearcherManager searcherManager;
            var luceneDirectory = configuration.Get("Local.Lucene.Directory");

            if (!string.IsNullOrEmpty(luceneDirectory))
            {
                string dataDirectory = configuration.Get("Local.Data.Directory");

                searcherManager = new NuGetSearcherManager(
                    luceneDirectory,
                    loggerFactory.CreateLogger<NuGetSearcherManager>(),
                    directory ?? new SimpleFSDirectory(new DirectoryInfo(luceneDirectory)),
                    loader ?? new FileLoader(dataDirectory));
            }
            else
            {
                string storagePrimary = configuration.Get("Storage.Primary");
                string indexContainer = configuration.Get("Search.IndexContainer");
                string dataContainer = configuration.Get("Search.DataContainer");

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storagePrimary);

                if (string.IsNullOrEmpty(indexContainer))
                {
                    indexContainer = "ng-search-index";
                }

                if (string.IsNullOrEmpty(dataContainer))
                {
                    dataContainer = "ng-search-data";
                }

                searcherManager = new NuGetSearcherManager(
                    indexContainer,
                    loggerFactory.CreateLogger<NuGetSearcherManager>(),
                    directory ?? new AzureDirectory(storageAccount, indexContainer),
                    loader ?? new StorageLoader(storageAccount, dataContainer, loggerFactory.CreateLogger<StorageLoader>()));
            }

            string registrationBaseAddress = configuration.Get("Search.RegistrationBaseAddress");
            searcherManager.RegistrationBaseAddress["http"] = MakeRegistrationBaseAddress("http", registrationBaseAddress);
            searcherManager.RegistrationBaseAddress["https"] = MakeRegistrationBaseAddress("https", registrationBaseAddress);

            return searcherManager;
        }

        internal static ILoggerFactory LoggerFactory { get; private set; }

        protected override IndexReader Reopen(IndexSearcher searcher)
        {
            _logger.LogInformation("NuGetSearcherManager.Reopen: refreshing original IndexReader.");

            var stopwatch = Stopwatch.StartNew();
            var indexReader = ((NuGetIndexSearcher)searcher).OriginalReader.Reopen();
            stopwatch.Stop();

            _logger.LogInformation("NuGetSearcherManager.Reopen: refreshed original IndexReader in {IndexReaderReopenDuration} seconds.", stopwatch.Elapsed.TotalSeconds);

            return indexReader;
        }

        /// <summary>
        /// This function is called whenever the SearcherManager decides it must re-create the IndexSearcher
        /// the key point to understand is that the auxillary data structures (in-memory indexes, filters and other lookups)
        /// absolutely must be kept in sync with the underlying IndexReader. This is because the shared key across
        /// all in-memory data is the Lucene docID and this can change following an index refresh.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected override NuGetIndexSearcher CreateSearcher(IndexReader reader)
        {
            _logger.LogInformation("NuGetSearcherManager.CreateSearcher");

            try
            {
                // (Re)load all the auxillary data
                // Just to keep things simple we will reload everything every time. Currently these structures are relative small.

                IDictionary<string, HashSet<string>> owners = IndexingUtils.Load("owners.json", _loader, _logger);
                IDictionary<string, HashSet<string>> cruratedfeeds = IndexingUtils.Load("curatedfeeds.json", _loader, _logger);
                IDictionary<string, IDictionary<string, int>> downloads = Downloads.Load("downloads.v1.json", _loader, _logger);
                IDictionary<string, int> rankings = DownloadRankings.Load("rankings.v1.json", _loader, _logger);

                // We want owners to be searchable so we need to have them in an IndexReader
                // To solve this we write them into an in-memory Directory and then create a reader over that
                // we can then combine this in-memory reader with the reader we have opened from storage

                var ownersHandler = new OwnersHandler(owners);
                var versionsHandler = new VersionsHandler(downloads);

                // The point of the IndexReaderProcess is to allow us to loop of the IndexReader fewer times.
                // Looping over the reader, accessing the Document and then accessing the fields inside the Document are not
                // inexpensive operations especially when you are going to do that for every Document in the index.

                IndexReaderProcessor auxillaryIndexProcessor = new IndexReaderProcessor(enumerateSubReaders: false, skipDeletes: false);
                auxillaryIndexProcessor.AddHandler(ownersHandler);
                auxillaryIndexProcessor.AddHandler(versionsHandler);

                auxillaryIndexProcessor.Process(reader);

                // These data structures are no longer needed as we now have the data in a format keyed on the docID

                downloads.Clear();
                owners.Clear();

                // Create the in-memory reader and then combine with the storage reader using a ParallelReader

                IndexReader ownersReader = ownersHandler.OpenReader();

                _logger.LogInformation("OwnersReader {MaxDoc} (deletes: {NumDeletedDocs})", ownersReader.MaxDoc, ownersReader.NumDeletedDocs);
                _logger.LogInformation("Original {MaxDoc} (deletes: {NumDeletedDocs})", reader.MaxDoc, reader.NumDeletedDocs);

                ParallelReader combined = new ParallelReader(false);
                combined.Add(reader);
                combined.Add(ownersReader);

                //Uncomment the following line to drop owners from the index...
                //IndexReader combined = reader;

                // Filters must be in terms of the structure of the underlying IndexReader. Specifically if the underlying
                // reader is Segmented then the filter must be too. Theoretically Lucene should be able to store a cached version of the
                // filter corresponding to each segment. We are not currently making use of that because we have introduced the ParallelReader

                // There are four flavors of Latest/Listed filter to reflect all the possible combinations.

                var h00 = new LatestListedHandler(includeUnlisted: false, includePrerelease: false);
                var h01 = new LatestListedHandler(includeUnlisted: false, includePrerelease: true);
                var h10 = new LatestListedHandler(includeUnlisted: true, includePrerelease: false);
                var h11 = new LatestListedHandler(includeUnlisted: true, includePrerelease: true);

                var curatedFeedHandler = new CuratedFeedHandler(cruratedfeeds);

                IndexReaderProcessor filterCreationProcessor = new IndexReaderProcessor(enumerateSubReaders: true, skipDeletes: true);
                filterCreationProcessor.AddHandler(h00);
                filterCreationProcessor.AddHandler(h01);
                filterCreationProcessor.AddHandler(h10);
                filterCreationProcessor.AddHandler(h11);
                filterCreationProcessor.AddHandler(curatedFeedHandler);

                filterCreationProcessor.Process(combined);

                cruratedfeeds.Clear();

                var latest = new Filter[][] { new Filter[] { h00.Result, h01.Result }, new Filter[] { h10.Result, h11.Result } };

                var latestBitSet = BitSetCollector.CreateBitSet(combined, latest[0][1]);
                var latestStableBitSet = BitSetCollector.CreateBitSet(combined, latest[0][0]);

                // The point of having a specific subclass of the IndexSearcher is that we want to associate a bunch of auxilliary data along
                // with that specific instance of the reader. The lifetimes are assocaited, hense the inheritance relationship.

                _logger.LogInformation("About to create a new NuGetIndexSearcher");

                // Create a NuGetIndexSearcher
                return new NuGetIndexSearcher(
                    this,
                    combined,
                    reader,
                    reader.CommitUserData,
                    curatedFeedHandler.Result,
                    latest,
                    versionsHandler.Result,
                    rankings,
                    latestBitSet,
                    latestStableBitSet);
            }
            catch (Exception e)
            {
                ServiceHelpers.TraceException(e, _logger);
                return null;
            }
        }

        protected override void Warm(NuGetIndexSearcher searcher)
        {
            // Warmup search
            searcher.Search(new MatchAllDocsQuery(), 1);
        }

        private static Uri MakeRegistrationBaseAddress(string scheme, string registrationBaseAddress)
        {
            Uri original = new Uri(registrationBaseAddress);
            if (original.Scheme == scheme)
            {
                return original;
            }
            else
            {
                return new UriBuilder(original)
                {
                    Scheme = scheme,
                    Port = -1
                }.Uri;
            }
        }
    }
}