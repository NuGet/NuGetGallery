// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Lucene;

namespace NuGetGallery.Infrastructure.Search
{
    public class ExternalSearchService : ISearchService, IIndexingService, IRawSearchService, IIndexingJobFactory
    {
        public static readonly string SearchRoundtripTimePerfCounter = "SearchRoundtripTime";
        private readonly ISearchClient _searchClient;

        private JObject _diagCache;

        protected IDiagnosticsSource Trace { get; private set; }

        public string IndexPath
        {
            get { return string.Empty ; }
        }

        public bool IsLocal
        {
            get { return false; }
        }

        public bool ContainsAllVersions { get { return true; } }

        public ExternalSearchService(IDiagnosticsService diagnostics, ISearchClient searchClient)
        {
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));

            Trace = diagnostics.SafeGetSource("ExternalSearchService");
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
            var result = await _searchClient.Search(
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

                results = new SearchResults(0, null, Enumerable.Empty<Package>().AsQueryable(), responseMessage: result.HttpResponse);
            }

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
                var resp = await _searchClient.GetDiagnostics();
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
