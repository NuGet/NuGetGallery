using Lucene.Net.Analysis;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Indexing
{
    public class SecureSearcherManager : SearcherManager
    {
        IDictionary<string, Filter> _filters;
        Filter _publicFilter;
        IDictionary<string, JArray[]> _versionsByDoc;
        JArray[] _versionListsByDoc;

        Filter _latestVersion;
        Filter _latestVersionIncludePrerelease;

        public string IndexName { get; private set; }
        public IDictionary<string, Uri> RegistrationBaseAddress { get; private set; }

        public DateTime LastReopen { get; private set; }

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

            _latestVersion = new CachingWrapperFilter(LatestVersionFilter.Create(searcher.IndexReader, false));
            _latestVersionIncludePrerelease = new CachingWrapperFilter(LatestVersionFilter.Create(searcher.IndexReader, true));

            // Recalculate precalculated Versions arrays 
            PackageVersions packageVersions = new PackageVersions(searcher.IndexReader);
            
            _versionsByDoc = new Dictionary<string, JArray[]>();
            _versionsByDoc["http"] = packageVersions.CreateVersionsLookUp(null, RegistrationBaseAddress["http"]);
            _versionsByDoc["https"] = packageVersions.CreateVersionsLookUp(null, RegistrationBaseAddress["https"]);

            _versionListsByDoc = packageVersions.CreateVersionListsLookUp();

            LastReopen = DateTime.UtcNow;
        }

        public Filter GetFilter(string tenantId, string[] types, bool includePrerelease)
        {
            Filter visibilityFilter = GetVisibilityFilter(tenantId);
            Filter typeFilter = new CachingWrapperFilter(new TypeFilter(types));
            Filter versionFilter = includePrerelease ? _latestVersionIncludePrerelease : _latestVersion;
            return new ChainedFilter(new Filter[] { visibilityFilter, versionFilter, typeFilter }, ChainedFilter.Logic.AND);
        }

        public Filter GetFilter(string tenantId, string[] types)
        {
            Filter visibilityFilter = GetVisibilityFilter(tenantId);
            Filter typeFilter = new CachingWrapperFilter(new TypeFilter(types));
            return new ChainedFilter(new Filter[] { visibilityFilter, typeFilter }, ChainedFilter.Logic.AND);
        }

        public Filter GetVisibilityFilter(string tenantId)
        {
            Filter tenantFilter;
            if (tenantId != null && _filters.TryGetValue(tenantId, out tenantFilter))
            {
                Filter chainedFilter = new ChainedFilter(new Filter[] { _publicFilter, tenantFilter }, ChainedFilter.Logic.OR);
                return chainedFilter;
            }
            else
            {
                return _publicFilter;
            }
        }

        public JArray GetVersions(string scheme, int doc)
        {
            return _versionsByDoc[scheme][doc];
        }

        public JArray GetVersionLists(int doc)
        {
            return _versionListsByDoc[doc];
        }
    }
}