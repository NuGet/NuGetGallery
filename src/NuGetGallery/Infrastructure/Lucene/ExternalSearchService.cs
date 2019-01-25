// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Search.Client;
using NuGet.Services.Search.Client.Correlation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Infrastructure.Lucene
{
    public class ExternalSearchService : ISearchService, IIndexingService, IRawSearchService
    {
        public static readonly string SearchRoundtripTimePerfCounter = "SearchRoundtripTime";

        private static IEndpointHealthIndicatorStore _healthIndicatorStore;
        // Search client that will be deprecated. It is still needed to allow the feature flag until the search that uses traffic manager is enabled.
        private static ISearchClient _deprecatedSearchClient;
        // Search client that will use the traffic manager end point.
        private ISearchClient _searchClient;

        private JObject _diagCache;

        public Uri ServiceUri { get; private set; }

        /// <summary>
        /// The SearchService Uri as set in the web.config.
        /// </summary>
        private Uri SearchServiceUri { get; set; }

        protected IDiagnosticsSource Trace { get; private set; }

        public string IndexPath
        {
            get { return ServiceUri.AbsoluteUri; }
        }

        public bool IsLocal
        {
            get { return false; }
        }

        public bool ContainsAllVersions { get { return true; } }

        public ExternalSearchService()
        {
            // used for testing
            if (_healthIndicatorStore == null)
            {
                _healthIndicatorStore = new BaseUrlHealthIndicatorStore(new NullHealthIndicatorLogger());
            }

            if (_deprecatedSearchClient == null)
            {
                _deprecatedSearchClient = new SearchClient(
                    ServiceUri, 
                    "SearchGalleryQueryService/3.0.0-rc", 
                    null, 
                    _healthIndicatorStore, 
                    QuietLog.LogHandledException, 
                    new TracingHttpHandler(Trace), 
                    new CorrelatingHttpClientHandler());
            }
        }

        public ExternalSearchService(IAppConfiguration config, IDiagnosticsService diagnostics, ISearchClient searchClient)
        {
            var serviceUri = config.ServiceDiscoveryUri;
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));

            Trace = diagnostics.SafeGetSource("ExternalSearchService");

            // Extract credentials
            ICredentials serviceUriCredentials = ExtractCredentialsFromUri(ref serviceUri);
            ServiceUri = serviceUri;

            // note: intentionally not locking the next two assignments to avoid blocking calls
            if (_healthIndicatorStore == null)
            {
                _healthIndicatorStore = new BaseUrlHealthIndicatorStore(new AppInsightsHealthIndicatorLogger());
            }

            if (_deprecatedSearchClient == null)
            {
                _deprecatedSearchClient = new SearchClient(
                    ServiceUri, 
                    config.SearchServiceResourceType,
                    serviceUriCredentials, 
                    _healthIndicatorStore,
                    QuietLog.LogHandledException,
                    new TracingHttpHandler(Trace), 
                    new CorrelatingHttpClientHandler());
            }
        }

        private static readonly Task<bool> _exists = Task.FromResult(true);

        public ICredentials ExtractCredentialsFromUri(ref Uri uri)
        {
            var userInfo = uri.UserInfo;
            ICredentials credentials = null;
            if (!String.IsNullOrEmpty(userInfo))
            {
                var split = userInfo.Split(':');
                if (split.Length != 2)
                {
                    throw new FormatException("Invalid user info in SearchServiceUri!");
                }

                // Split the credentials out
                credentials = new NetworkCredential(split[0], split[1]);
                uri = new UriBuilder(ServiceUri)
                {
                    UserName = null,
                    Password = null
                }.Uri;
            }

            return credentials;
        }

        public Task<bool> Exists()
        {
            return _exists;
        }

        public virtual Task<SearchResults> RawSearch(SearchFilter filter)
        {
            return SearchCore(filter, raw: true);
        }

        public virtual Task<SearchResults> Search(SearchFilter filter)
        {
            return SearchCore(filter, raw: false);
        }

        private async Task<SearchResults> SearchCore(SearchFilter filter, bool raw)
        {
            // Query!
            var sw = new Stopwatch();
            sw.Start();
            var result = await GetClient().Search(
                filter.SearchTerm,
                projectTypeFilter: null,
                includePrerelease: filter.IncludePrerelease,
                sortBy: filter.SortOrder,
                skip: filter.Skip,
                take: filter.Take,
                isLuceneQuery: raw,
                countOnly: filter.CountOnly,
                explain: false,
                getAllVersions: filter.IncludeAllVersions,
                supportedFramework: filter.SupportedFramework,
                semVerLevel: filter.SemVerLevel);
            sw.Stop();

            SearchResults results = null;
            if (result.IsSuccessStatusCode)
            {
                var content = await result.ReadContent();
                if (content == null)
                {
                    results = new SearchResults(0, null, Enumerable.Empty<Package>().AsQueryable());
                } 
                else if (filter.CountOnly || content.TotalHits == 0)
                {
                    results = new SearchResults(content.TotalHits, content.IndexTimestamp);
                }
                else
                {
                    results = new SearchResults(
                        content.TotalHits,
                        content.IndexTimestamp,
                        content.Data.Select(x => ReadPackage(x, filter.SemVerLevel)).AsQueryable());
                }
            }
            else
            {
                if (result.HttpResponse.Content != null)
                {
                    result.HttpResponse.Content.Dispose();
                }

                results = new SearchResults(0, null, Enumerable.Empty<Package>().AsQueryable());
            }

            Trace.PerfEvent(
                SearchRoundtripTimePerfCounter,
                sw.Elapsed,
                new Dictionary<string, object>() {
                    {"Term", filter.SearchTerm},
                    {"Context", filter.Context},
                    {"Raw", raw},
                    {"Hits", results == null ? -1 : results.Hits},
                    {"StatusCode", (int)result.StatusCode},
                    {"SortOrder", filter.SortOrder.ToString()},
                    {"Url", TryGetUrl()}
                });

            return results;
        }

        private static string TryGetUrl()
        {
            return HttpContext.Current != null ?
                HttpContext.Current.Request.Url.AbsoluteUri :
                String.Empty;
        }

        public async Task<DateTime?> GetLastWriteTime()
        {
            await EnsureDiagnostics();
            var commitData = _diagCache["CommitUserData"];
            if (commitData != null)
            {
                var timeStamp = commitData["commit-time-stamp"];
                if (timeStamp != null)
                {
                    return DateTime.Parse(timeStamp.Value<string>());
                }
            }
            return null;
        }

        public async Task<long> GetIndexSizeInBytes()
        {
            await EnsureDiagnostics();
            var totalMemory = _diagCache["TotalMemory"];
            if (totalMemory != null)
            {
                return totalMemory.Value<long>();
            }
            return 0;
        }

        public async Task<int> GetDocumentCount()
        {
            await EnsureDiagnostics();
            var numDocs = _diagCache["NumDocs"];
            if (numDocs != null)
            {
                return numDocs.Value<int>();
            }
            return 0;
        }

        private async Task EnsureDiagnostics()
        {
            if (_diagCache == null)
            {
                var resp = await GetClient().GetDiagnostics();
                if (!resp.IsSuccessStatusCode)
                {
                    Trace.Error("HTTP Error when retrieving diagnostics: " + ((int)resp.StatusCode).ToString());
                    _diagCache = new JObject();
                }
                else
                {
                    _diagCache = await resp.ReadContent();
                }
            }
        }

        internal static Package ReadPackage(JObject doc, string semVerLevel)
        {
            var dependencies =
                doc.Value<JArray>("Dependencies")
                   .Cast<JObject>()
                   .Select(obj => new PackageDependency()
                    {
                        Id = obj.Value<string>("Id"),
                        VersionSpec = obj.Value<string>("VersionSpec"),
                        TargetFramework = obj.Value<string>("TargetFramework")
                    })
                   .ToArray();

            var frameworks =
                doc.Value<JArray>("SupportedFrameworks")
                   .Select(v => new PackageFramework() { TargetFramework = v.Value<string>() })
                   .ToArray();

            var reg = doc["PackageRegistration"];
            PackageRegistration registration = null;
            if(reg != null) {
                registration = new PackageRegistration() {
                    Id = reg.Value<string>("Id"),
                    Owners = reg.Value<JArray>("Owners")
                       .Select(v => new User { Username = v.Value<string>() })
                       .ToArray(),
                    DownloadCount = reg.Value<int>("DownloadCount"),
                    IsVerified = reg.Value<bool>("Verified"),
                    Key = reg.Value<int>("Key")
                };
            }

            var isLatest = doc.Value<bool>("IsLatest");
            var isLatestStable = doc.Value<bool>("IsLatestStable");
            var semVer2 = SemVerLevelKey.ForSemVerLevel(semVerLevel) == SemVerLevelKey.SemVer2;

            return new Package
            {
                Copyright = doc.Value<string>("Copyright"),
                Created = doc.Value<DateTime>("Created"),
                Description = doc.Value<string>("Description"),
                Dependencies = dependencies,
                DownloadCount = doc.Value<int>("DownloadCount"),
                FlattenedAuthors = doc.Value<string>("Authors"),
                FlattenedDependencies = doc.Value<string>("FlattenedDependencies"),
                Hash = doc.Value<string>("Hash"),
                HashAlgorithm = doc.Value<string>("HashAlgorithm"),
                IconUrl = doc.Value<string>("IconUrl"),
                IsLatest = isLatest,
                IsLatestStable = isLatestStable,
                IsLatestSemVer2 = semVer2 ? isLatest : false,
                IsLatestStableSemVer2 = semVer2 ? isLatestStable : false,
                Key = doc.Value<int>("Key"),
                Language = doc.Value<string>("Language"),
                LastUpdated = doc.Value<DateTime>("LastUpdated"),
                LastEdited = doc.Value<DateTime?>("LastEdited"),
                PackageRegistration = registration,
                PackageRegistrationKey = registration?.Key ?? 0,
                PackageFileSize = doc.Value<long>("PackageFileSize"),
                ProjectUrl = doc.Value<string>("ProjectUrl"),
                Published = doc.Value<DateTime>("Published"),
                ReleaseNotes = doc.Value<string>("ReleaseNotes"),
                RequiresLicenseAcceptance = doc.Value<bool>("RequiresLicenseAcceptance"),
                Summary = doc.Value<string>("Summary"),
                Tags = doc.Value<string>("Tags"),
                Title = doc.Value<string>("Title"),
                Version = doc.Value<string>("Version"),
                NormalizedVersion = doc.Value<string>("NormalizedVersion"),
                SupportedFrameworks = frameworks,
                MinClientVersion = doc.Value<string>("MinClientVersion"),
                LicenseUrl = doc.Value<string>("LicenseUrl"),
                LicenseNames = doc.Value<string>("LicenseNames"),
                LicenseReportUrl = doc.Value<string>("LicenseReportUrl"),
                HideLicenseReport = doc.Value<bool>("HideLicenseReport"),
                Listed = doc.Value<bool>("Listed")
            };
        }

        /// <summary>
        /// It will return the client to use based on the feature flag.
        /// </summary>
        /// <returns></returns>
        public ISearchClient GetClient(){ return _searchClient; }

        // Bunch of no-ops to disable indexing because an external search service is doing that.
        public void UpdateIndex()
        {
            // No-op
        }

        public void UpdateIndex(bool forceRefresh)
        {
            // No-op
        }

        public void UpdatePackage(Package package)
        {
            // No-op
        }

        public void RegisterBackgroundJobs(IList<WebBackgrounder.IJob> jobs, IAppConfiguration configuration)
        {
            // No background jobs to register!
        }
    }
}
