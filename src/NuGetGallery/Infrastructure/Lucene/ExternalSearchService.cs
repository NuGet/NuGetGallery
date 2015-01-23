using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using NuGet.Services.Search.Client;
using NuGet.Services.Search.Models;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Infrastructure.Lucene
{
    public class ExternalSearchService : ISearchService, IIndexingService, IRawSearchService
    {
        public static readonly string SearchRoundtripTimePerfCounter = "SearchRoundtripTime";
        
        private SearchClient _client;
        private JObject _diagCache;

        public Uri ServiceUri { get; private set; }
        
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

        public ExternalSearchService(IAppConfiguration config, IDiagnosticsService diagnostics)
        {
            ServiceUri = config.ServiceDiscoveryUri;

            Trace = diagnostics.SafeGetSource("ExternalSearchService");

            // Extract credentials
            var userInfo = ServiceUri.UserInfo;
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
                ServiceUri = new UriBuilder(ServiceUri)
                {
                    UserName = null,
                    Password = null
                }.Uri;
            }

            _client = new SearchClient(ServiceUri, config.SearchServiceResourceType, credentials, new TracingHttpHandler(Trace));
        }

        private static readonly Task<bool> _exists = Task.FromResult(true);
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification="This method is implementing an interface")]
        public Task<bool> Exists()
        {
            return _exists;
        }

        public Task<SearchResults> RawSearch(SearchFilter filter)
        {
            return SearchCore(filter, raw: true);
        }

        public Task<SearchResults> Search(SearchFilter filter)
        {
            return SearchCore(filter, raw: false);
        }

        private async Task<SearchResults> SearchCore(SearchFilter filter, bool raw)
        {
            // Query!
            var sw = new Stopwatch();
            sw.Start();
            var result = await _client.Search(
                filter.SearchTerm,
                projectTypeFilter: null,
                includePrerelease: filter.IncludePrerelease,
                curatedFeed: filter.CuratedFeed == null ? null : filter.CuratedFeed.Name,
                sortBy: filter.SortOrder,
                skip: filter.Skip,
                take: filter.Take,
                isLuceneQuery: raw,
                countOnly: filter.CountOnly,
                explain: false,
                getAllVersions: filter.IncludeAllVersions,
                supportedFramework: filter.SupportedFramework);
            sw.Stop();

            SearchResults results = null;
            if (result.IsSuccessStatusCode)
            {
                var content = await result.ReadContent();
                if (filter.CountOnly || content.TotalHits == 0)
                {
                    results = new SearchResults(content.TotalHits, content.IndexTimestamp);
                }
                else
                {
                    results = new SearchResults(
                        content.TotalHits,
                        content.IndexTimestamp,
                        content.Data.Select(ReadPackage).AsQueryable());
                }
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
                    {"CuratedFeed", filter.CuratedFeed == null ? null : filter.CuratedFeed.Name},
                    {"Url", TryGetUrl()}
                });

            result.HttpResponse.EnsureSuccessStatusCode();
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
                var resp = await _client.GetDiagnostics();
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

        private static Package ReadPackage(JObject doc)
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
                    Key = reg.Value<int>("Key")
                };
            }

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
                IsLatest = doc.Value<bool>("IsLatest"),
                IsLatestStable = doc.Value<bool>("IsLatestStable"),
                Key = doc.Value<int>("Key"),
                Language = doc.Value<string>("Language"),
                LastUpdated = doc.Value<DateTime>("LastUpdated"),
                LastEdited = doc.Value<DateTime?>("LastEdited"),
                PackageRegistration = registration,
                PackageRegistrationKey = registration == null ? 0 : registration.Key,
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
                HideLicenseReport = doc.Value<bool>("HideLicenseReport")
            };
        }

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
