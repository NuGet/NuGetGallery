// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Indexing
{
    public class NuGetSearcherManager : SearcherManager<NuGetIndexSearcher>
    {
        ILoader _loader;

        public NuGetSearcherManager(string indexName, Lucene.Net.Store.Directory directory, ILoader loader)
            : base(directory)
        {
            IndexName = indexName;
            RegistrationBaseAddress = new Dictionary<string, Uri>();

            _loader = loader;
        }

        public string IndexName { get; private set; }
        public IDictionary<string, Uri> RegistrationBaseAddress { get; private set; }

        // /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static NuGetSearcherManager CreateAzure(string storagePrimary, string indexContainer, string dataContainer)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storagePrimary);

            if (String.IsNullOrEmpty(indexContainer))
            {
                indexContainer = "ng-search-index";
            }

            if (String.IsNullOrEmpty(dataContainer))
            {
                dataContainer = "ng-search-data";
            }

            return new NuGetSearcherManager(
                indexContainer,
                new AzureDirectory(storageAccount, indexContainer, new RAMDirectory()),
                new StorageLoader(storageAccount, dataContainer));
        }

        // /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static NuGetSearcherManager CreateLocal(string luceneDirectory, string dataDirectory)
        {
            return new NuGetSearcherManager(
                luceneDirectory,
                new SimpleFSDirectory(new DirectoryInfo(luceneDirectory)),
                new FileLoader(dataDirectory));
        }

        // ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

        protected override NuGetIndexSearcher CreateSearcher(IndexReader reader)
        {
            IDictionary<string, HashSet<string>> owners = Utils.Load("owners.json", _loader);
            IDictionary<string, HashSet<string>> cruratedfeeds = Utils.Load("curatedfeeds.json", _loader);
            IDictionary<string, IDictionary<string, int>> downloads = Downloads.Load("downloads.v1.json", _loader);
            IDictionary<string, int> rankings = DownloadRankings.Load("rankings.v1.json", _loader);

            var ownersHandler = new OwnersHandler(owners);
            var versionsHandler = new VersionsHandler(downloads);

            IndexReaderProcessor auxillaryIndexProcessor = new IndexReaderProcessor(enumerateSubReaders: false, skipDeletes: false);
            auxillaryIndexProcessor.AddHandler(ownersHandler);
            auxillaryIndexProcessor.AddHandler(versionsHandler);

            auxillaryIndexProcessor.Process(reader);

            downloads.Clear();
            owners.Clear();

            IndexReader ownersReader = ownersHandler.OpenReader();

            ParallelReader combined = new ParallelReader(false);
            combined.Add(reader);
            combined.Add(ownersReader);

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

            // Create a NuGetIndexSearcher
            return new NuGetIndexSearcher(
                this,
                combined,
                reader.CommitUserData,
                curatedFeedHandler.Result, latest,
                versionsHandler.Result,
                rankings,
                latestBitSet,
                latestStableBitSet);
        }

        protected override void Warm(NuGetIndexSearcher searcher)
        {
            // Warmup search
            searcher.Search(new MatchAllDocsQuery(), 1);
        }
    }
}