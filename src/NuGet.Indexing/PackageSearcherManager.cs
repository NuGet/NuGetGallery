// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Indexing
{
    public class PackageSearcherManager : SearcherManager
    {
        public static readonly TimeSpan FrameworksRefreshRate = TimeSpan.FromHours(24);
        public static readonly TimeSpan PortableFrameworksRefreshRate = TimeSpan.FromHours(24);
        public static readonly TimeSpan RankingRefreshRate = TimeSpan.FromHours(24);
        public static readonly TimeSpan DownloadCountRefreshRate = TimeSpan.FromHours(1);

        IndexData<IDictionary<string, IDictionary<string, int>>> _currentRankings;
        IndexData<IDictionary<string, IDictionary<string, int>>> _currentDownloadCounts;
        IndexData<IList<FrameworkName>> _currentFrameworkList;

        public DateTime DownloadCountsUpdatedUtc { get { return _currentDownloadCounts.LastUpdatedUtc; } }
        public DateTime RankingsUpdatedUtc { get { return _currentRankings.LastUpdatedUtc; } }
        public DateTime FrameworkListUpdatedUtc { get { return _currentFrameworkList.LastUpdatedUtc; } }
        public Rankings Rankings { get; private set; }
        public DownloadLookup DownloadCounts { get; private set; }
        public FrameworksList Frameworks { get; private set; }
        public Guid Id { get; private set; }
        public string IndexName { get; private set; }

        public event EventHandler<SegmentInfoEventArgs> IndexReopened;

        protected PackageSearcherManager(string indexName, Lucene.Net.Store.Directory directory, Rankings rankings, DownloadLookup downloadCounts, FrameworksList frameworks)
            : base(directory)
        {
            Rankings = rankings;
            DownloadCounts = downloadCounts;
            Frameworks = frameworks;
            IndexName = indexName;      

            _currentDownloadCounts = new IndexData<IDictionary<string, IDictionary<string, int>>>(
                "DownloadCounts",
                DownloadCounts.Path,
                DownloadCounts.Load,
                DownloadCountRefreshRate);
            _currentRankings = new IndexData<IDictionary<string, IDictionary<string, int>>>(
                "Rankings",
                Rankings.Path,
                Rankings.Load,
                RankingRefreshRate);
            _currentFrameworkList = new IndexData<IList<FrameworkName>>(
                "FrameworkList",
                Frameworks.Path,
                Frameworks.Load,
                FrameworksRefreshRate);

            Id = Guid.NewGuid(); // Used for identifying changes to the searcher manager at runtime.
        }

        protected override void Warm(IndexSearcher searcher)
        {
            searcher.Search(new MatchAllDocsQuery(), 1);

            // Reload download counts and rankings synchronously
            _currentDownloadCounts.Reload();
            _currentRankings.Reload();
            _currentFrameworkList.Reload();

            // If someone's interested in our segment info, let them have it
            if (IndexReopened != null)
            {
                var segments = new List<SegmentInfoEventArgs.SegmentInfo>();
                foreach (ReadOnlySegmentReader readOnlySegmentReader in searcher.IndexReader.GetSequentialSubReaders())
                {
                    segments.Add(new SegmentInfoEventArgs.SegmentInfo
                    {
                        Name = readOnlySegmentReader.SegmentName,
                        NumDocs = readOnlySegmentReader.NumDocs()
                    });
                }

                IndexReopened(this, new SegmentInfoEventArgs(segments.AsReadOnly()));
            }
        }

        public IDictionary<string, int> GetRankings(string context)
        {
            _currentRankings.MaybeReload();

            // Capture the current value
            var tempRankings = _currentRankings.Value;

            if (tempRankings == null)
            {
                return new Dictionary<string, int>();
            }

            IDictionary<string, int> rankings;
            if (tempRankings.TryGetValue(context, out rankings))
            {
                return rankings;
            }

            return tempRankings["Rank"];
        }

        public Tuple<int,int> GetDownloadCount(string packageId,string normalizedVersion)
        {
            _currentDownloadCounts.MaybeReload();

            // Capture the current value and use it
            var downloadCounts = _currentDownloadCounts.Value;
            if (downloadCounts != null)
            {                                
                IDictionary<string,int> versions;
                if (downloadCounts.TryGetValue(packageId.ToLowerInvariant(), out versions))
                {
                    int registrationDownloads = versions.Values.Sum();                    
                    int downloadCount;
                    if(!versions.TryGetValue(normalizedVersion,out downloadCount))
                    {
                        //assume value as 0 if version not found.
                        downloadCount = 0;
                    }
                    return new Tuple<int, int>(downloadCount, registrationDownloads);
                }                
            }
            return new Tuple<int,int>(0,0);
        }

        public IList<FrameworkName> GetFrameworks()
        {
            _currentFrameworkList.MaybeReload();

            // Return the current value. It may be swapped out from under us but that's OK.
            return _currentFrameworkList.Value ?? new List<FrameworkName>();
        }

        public static PackageSearcherManager CreateLocal(
            string localDirectory,
            string frameworksFile = null,
            string rankingsFile = null,
            string downloadCountsFile = null)
        {
            if (String.IsNullOrEmpty(frameworksFile))
            {
                frameworksFile = Path.Combine(localDirectory, "data", FrameworksList.FileName);
            }
            if (String.IsNullOrEmpty(rankingsFile))
            {
                rankingsFile = Path.Combine(localDirectory, "data", Rankings.FileName);
            }
            if (String.IsNullOrEmpty(downloadCountsFile))
            {
                downloadCountsFile = Path.Combine(localDirectory, "data", DownloadLookup.FileName);
            }
            var dir = new DirectoryInfo(localDirectory);
            return new PackageSearcherManager(
                dir.Name,
                new SimpleFSDirectory(dir),
                new LocalRankings(rankingsFile),
                new LocalDownloadLookup(downloadCountsFile),
                new LocalFrameworksList(frameworksFile));
        }

        public static PackageSearcherManager CreateAzure(
            string storageConnectionString,
            string indexContainer = null,
            string dataContainer = null)
        {
            return CreateAzure(
                CloudStorageAccount.Parse(storageConnectionString),
                indexContainer,
                dataContainer);
        }
        public static PackageSearcherManager CreateAzure(
            CloudStorageAccount storageAccount,
            string indexContainer = null,
            string dataContainer = null,
            bool requireDownloadCounts = true)
        {
            if (String.IsNullOrEmpty(indexContainer))
            {
                indexContainer = "ng-search-index";
            }

            string dataPath = String.Empty;
            if (String.IsNullOrEmpty(dataContainer))
            {
                dataContainer = indexContainer;
                dataPath = "data/";
            }

            DownloadLookup downloadCounts = requireDownloadCounts
                    ?
                (DownloadLookup)new StorageDownloadLookup(storageAccount, dataContainer, dataPath + DownloadLookup.FileName)
                    :
                (DownloadLookup)new LocalDownloadLookup(null);

            return new PackageSearcherManager(
                indexContainer,
                new AzureDirectory(storageAccount, indexContainer, new RAMDirectory()),
                new StorageRankings(storageAccount, dataContainer, dataPath + Rankings.FileName),
                downloadCounts,
                new StorageFrameworksList(storageAccount, dataContainer, dataPath + FrameworksList.FileName));
        }

        // Little helper class to handle these "load async and swap" objects
        private class IndexData<T> where T : class
        {
            private Func<T> _loader;
            private object _lock = new object();
            private T _value;

            public string Name { get; private set; }
            public string Path { get; private set; }
            public T Value { get { return _value; } }
            public DateTime LastUpdatedUtc { get; private set; }
            public TimeSpan UpdateInterval { get; private set; }
            public bool Updating { get; private set; }

            public IndexData(string name, string path, Func<T> loader, TimeSpan updateInterval)
            {
                _loader = loader;

                Name = name;
                Path = path;
                LastUpdatedUtc = DateTime.MinValue;
                UpdateInterval = updateInterval;
                Updating = false;
            }

            public void MaybeReload()
            {
                lock (_lock)
                {
                    if ((Value == null || ((DateTime.UtcNow - LastUpdatedUtc) > UpdateInterval)) && !Updating)
                    {
                        // Start updating
                        Updating = true;
                        Task.Factory.StartNew(Reload);
                    }
                }
            }

            public void Reload()
            {
                IndexingEventSource.Log.ReloadingData(Name, Path);
                var newValue = _loader();
                lock (_lock)
                {
                    Updating = false;
                    LastUpdatedUtc = DateTime.UtcNow;

                    // The lock doesn't cover Value, so we need to change it using Interlocked.Exchange.
                    Interlocked.Exchange(ref _value, newValue);
                }
                IndexingEventSource.Log.ReloadedData(Name);
            }
        }
    }
}