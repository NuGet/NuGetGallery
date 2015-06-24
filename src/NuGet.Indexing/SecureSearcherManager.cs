// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Index;

namespace NuGet.Indexing
{
    public class SecureSearcherManager : SearcherManager, ISearchIndexInfo
    {
        IDictionary<string, Filter> _filters;
        Filter _publicFilter;

        JArray[] _versionsByDoc;
        JArray[] _versionListsByDoc;

        Filter _latestVersion;
        Filter _latestVersionIncludeUnlisted;
        Filter _latestVersionIncludePrerelease;
        Filter _latestVersionIncludePrereleaseIncludeUnlisted;

        public string IndexName { get; private set; }
        public IDictionary<string, Uri> RegistrationBaseAddress { get; private set; }

        public DateTime LastReopen { get; private set; }

        public int NumDocs { get; private set; }
        public IDictionary<string, string> CommitUserData { get; private set; }

        public SecureSearcherManager(string indexName, Lucene.Net.Store.Directory directory)
            : base(directory)
        {
            IndexName = indexName;

            RegistrationBaseAddress = new Dictionary<string, Uri>();
        }

        public static SecureSearcherManager CreateLocal(string path)
        {
            return new SecureSearcherManager(path, new SimpleFSDirectory(new DirectoryInfo(path)));
        }

        public static SecureSearcherManager CreateAzure(string storagePrimary, string searchIndexContainer)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storagePrimary);
            return new SecureSearcherManager(searchIndexContainer, new AzureDirectory(storageAccount, searchIndexContainer, new RAMDirectory()));
        }

        // ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

        protected override void Warm(IndexSearcher searcher)
        {
            searcher.Search(new MatchAllDocsQuery(), 1);

            // Create the tenant filters
            _filters = new Dictionary<string, Filter>();
            IEnumerable<string> tenantIds = PackageTenantId.GetDistintTenantId(searcher.IndexReader);
            foreach (string tenantId in tenantIds)
            {
                _filters.Add(tenantId, new CachingWrapperFilter(new TenantFilter(tenantId)));
            }
            _publicFilter = new CachingWrapperFilter(new PublicFilter());

            _latestVersion = new CachingWrapperFilter(LatestVersionFilterFactory.Create(searcher.IndexReader, false, false));
            _latestVersionIncludeUnlisted = new CachingWrapperFilter(LatestVersionFilterFactory.Create(searcher.IndexReader, false, true));
            _latestVersionIncludePrerelease = new CachingWrapperFilter(LatestVersionFilterFactory.Create(searcher.IndexReader, true, false));
            _latestVersionIncludePrereleaseIncludeUnlisted = new CachingWrapperFilter(LatestVersionFilterFactory.Create(searcher.IndexReader, true, true));

            // Recalculate precalculated Versions arrays 
            PackageVersions packageVersions = new PackageVersions(searcher.IndexReader);
            _versionsByDoc = packageVersions.CreateVersionsLookUp(null);
            _versionListsByDoc = packageVersions.CreateVersionListsLookUp();

            // Set metadata
            LastReopen = DateTime.UtcNow;
            NumDocs = searcher.IndexReader.NumDocs();
            CommitUserData = searcher.IndexReader.CommitUserData;
        }

        public Filter GetFilter(string tenantId, string[] types, bool includePrerelease, bool includeUnlisted)
        {
            Filter visibilityFilter = GetVisibilityFilter(tenantId);
            Filter typeFilter = new CachingWrapperFilter(new TypeFilter(types));
            Filter versionFilter = GetVersionFilter(includePrerelease, includeUnlisted);
            
            return new ChainedFilter(new[] { visibilityFilter, versionFilter, typeFilter }, ChainedFilter.Logic.AND);
        }

        public Filter GetFilter(string tenantId, string[] types)
        {
            Filter visibilityFilter = GetVisibilityFilter(tenantId);
            Filter typeFilter = new CachingWrapperFilter(new TypeFilter(types));

            return new ChainedFilter(new[] { visibilityFilter, typeFilter }, ChainedFilter.Logic.AND);
        }

        Filter GetVersionFilter(bool includePrerelease, bool includeUnlisted)
        {
            return includePrerelease
                    ?
                (includeUnlisted ? _latestVersionIncludePrereleaseIncludeUnlisted : _latestVersionIncludePrerelease)
                    :
                (includeUnlisted ? _latestVersionIncludeUnlisted : _latestVersion);
        }

        Filter GetVisibilityFilter(string tenantId)
        {
            Filter tenantFilter;
            if (tenantId != null && _filters.TryGetValue(tenantId, out tenantFilter))
            {
                Filter chainedFilter = new ChainedFilter(new[] { _publicFilter, tenantFilter }, ChainedFilter.Logic.OR);
                return chainedFilter;
            }
            else
            {
                return _publicFilter;
            }
        }

        public JArray GetVersions(string scheme, int doc)
        {
            var baseUrl = RegistrationBaseAddress[scheme];
            var versions = _versionsByDoc[doc].DeepClone() as JArray;
            foreach (var version in versions)
            {
                version["@id"] = new Uri(baseUrl, version["@id"].ToString()).AbsoluteUri;
            }
            return versions;
        }

        public JArray GetVersionLists(int doc)
        {
            return _versionListsByDoc[doc];
        }
        
        public Dictionary<string, int> GetSegments()
        {
            var searcher = Get();
            try
            {
                var reader = searcher.IndexReader;

                Dictionary<string, int> segments = new Dictionary<string, int>();

                foreach (var indexReader in reader.GetSequentialSubReaders())
                {
                    var segmentReader = (ReadOnlySegmentReader)indexReader;
                    segments.Add(segmentReader.SegmentName, segmentReader.NumDocs());
                }

                return segments;
            }
            finally
            {
                Release(searcher);
            }
        }
    }
}