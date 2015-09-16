// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using NuGet;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public partial class ApiController
        : AppController
    {
        private const string _idKey = "id";
        private const string _versionKey = "version";
        private const string _ipAddressKey = "ipAddress";
        private const string _userAgentKey = "userAgent";
        private const string _operationKey = "operation";
        private const string _dependentPackageKey = "dependentPackage";
        private const string _projectGuidsKey = "projectGuids";
        private const string _metricsDownloadEventMethod = "/DownloadEvent";
        private const string _contentTypeJson = "application/json";
        private readonly IAppConfiguration _config;

        public IEntitiesContext EntitiesContext { get; set; }
        public INuGetExeDownloaderService NugetExeDownloaderService { get; set; }
        public IPackageFileService PackageFileService { get; set; }
        public IPackageService PackageService { get; set; }
        public IUserService UserService { get; set; }
        public IStatisticsService StatisticsService { get; set; }
        public IContentService ContentService { get; set; }
        public ISearchService SearchService { get; set; }
        public IIndexingService IndexingService { get; set; }
        public IAutomaticallyCuratePackageCommand AutoCuratePackage { get; set; }
        public IStatusService StatusService { get; set; }

        protected ApiController()
        {
        }

        public ApiController(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IContentService contentService,
            IIndexingService indexingService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratePackage,
            IStatusService statusService,
            IAppConfiguration config)
        {
            EntitiesContext = entitiesContext;
            PackageService = packageService;
            PackageFileService = packageFileService;
            UserService = userService;
            NugetExeDownloaderService = nugetExeDownloaderService;
            ContentService = contentService;
            StatisticsService = null;
            IndexingService = indexingService;
            SearchService = searchService;
            AutoCuratePackage = autoCuratePackage;
            StatusService = statusService;
            _config = config;
        }

        public ApiController(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IContentService contentService,
            IIndexingService indexingService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratePackage,
            IStatusService statusService,
            IStatisticsService statisticsService,
            IAppConfiguration config)
            : this(entitiesContext, packageService, packageFileService, userService, nugetExeDownloaderService, contentService, indexingService, searchService, autoCuratePackage, statusService, config)
        {
            StatisticsService = statisticsService;
        }

        [HttpGet]
        [ActionName("GetPackageApi")]
        public virtual async Task<ActionResult> GetPackage(string id, string version)
        {
            // some security paranoia about URL hacking somehow creating e.g. open redirects
            // validate user input: explicit calls to the same validators used during Package Registrations
            // Ideally shouldn't be necessary?
            if (!PackageIdValidator.IsValidPackageId(id ?? ""))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The format of the package id is invalid");
            }

            // if version is non-null, check if it's semantically correct and normalize it.
            if (!String.IsNullOrEmpty(version))
            {
                SemanticVersion dummy;
                if (!SemanticVersion.TryParse(version, out dummy))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The package version is not a valid semantic version");
                }
                // Normalize the version
                version = SemanticVersionExtensions.Normalize(version);
            }
            else
            {
                // if version is null, get the latest version from the database.
                // This ensures that on package restore scenario where version will be non null, we don't hit the database.
                try
                {
                    var package = PackageService.FindPackageByIdAndVersion(id, version, allowPrerelease: false);
                    if (package == null)
                    {
                       return new HttpStatusCodeWithBodyResult(HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                    }
                    version = package.NormalizedVersion;

                }
                catch (SqlException e)
                {
                    QuietLog.LogHandledException(e);

                    // Database was unavailable and we don't have a version, return a 503
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
                }
                catch (DataException e)
                {
                    QuietLog.LogHandledException(e);

			        // Database was unavailable and we don't have a version, return a 503
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
                }

            }

            // If metrics service is specified we post the data to it asynchronously. Else we skip stats.
            if (_config != null && _config.MetricsServiceUri != null)
            {
                try
                {
                    var userHostAddress = Request.UserHostAddress;
                    var userAgent = Request.UserAgent;
                    var operation = Request.Headers["NuGet-Operation"];
                    var dependentPackage = Request.Headers["NuGet-DependentPackage"];
                    var projectGuids = Request.Headers["NuGet-ProjectGuids"];

                    HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => PostDownloadStatistics(id, version, userHostAddress, userAgent, operation, dependentPackage, projectGuids, cancellationToken));
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return await PackageFileService.CreateDownloadPackageActionResultAsync(
                HttpContext.Request.Url,
                id, version);
        }

        private static JObject GetJObject(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = new JObject();
            jObject.Add(_idKey, id);
            jObject.Add(_versionKey, version);
            if (!String.IsNullOrEmpty(ipAddress)) jObject.Add(_ipAddressKey, ipAddress);
            if (!String.IsNullOrEmpty(userAgent)) jObject.Add(_userAgentKey, userAgent);
            if (!String.IsNullOrEmpty(operation)) jObject.Add(_operationKey, operation);
            if (!String.IsNullOrEmpty(dependentPackage)) jObject.Add(_dependentPackageKey, dependentPackage);
            if (!String.IsNullOrEmpty(projectGuids)) jObject.Add(_projectGuidsKey, projectGuids);

            return jObject;
        }

        private async Task PostDownloadStatistics(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids, CancellationToken cancellationToken)
        {
            if (_config == null || _config.MetricsServiceUri == null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var jObject = GetJObject(id, version, ipAddress, userAgent, operation, dependentPackage, projectGuids);

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    await httpClient.PostAsync(new Uri(_config.MetricsServiceUri, _metricsDownloadEventMethod), new StringContent(jObject.ToString(), Encoding.UTF8, _contentTypeJson), cancellationToken);
                }
            }
            catch (WebException ex)
            {
                QuietLog.LogHandledException(ex);
            }
            catch (AggregateException ex)
            {
                QuietLog.LogHandledException(ex.InnerException ?? ex);
            }
            catch (TaskCanceledException)
            {
                // noop
            }
        }


        [HttpGet]
        [ActionName("GetNuGetExeApi")]
        [RequireHttps]
        [OutputCache(VaryByParam = "none", Location = OutputCacheLocation.ServerAndClient, Duration = 600)]
        public virtual Task<ActionResult> GetNuGetExe()
        {
            return NugetExeDownloaderService.CreateNuGetExeDownloadActionResultAsync(HttpContext.Request.Url);
        }

        [HttpGet]
        [ActionName("StatusApi")]
        public async virtual Task<ActionResult> Status()
        {
            if (StatusService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Status service is unavailable");
            }
            return await StatusService.GetStatus();
        }

        [HttpGet]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("VerifyPackageKey")]
        public virtual ActionResult VerifyPackageKey(string id, string version)
        {
            if (!String.IsNullOrEmpty(id))
            {
                // If the partialId is present, then verify that the user has permission to push for the specific Id \ version combination.
                var package = PackageService.FindPackageByIdAndVersion(id, version);
                if (package == null)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                }

                var user = GetCurrentUser();
                if (!package.IsOwner(user))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                }
            }

            return new EmptyResult();
        }

        [HttpPut]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePut()
        {
            return CreatePackageInternal();
        }

        [HttpPost]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePost()
        {
            return CreatePackageInternal();
        }

        private async Task<ActionResult> CreatePackageInternal()
        {
            // Get the user
            var user = GetCurrentUser();

            using (var packageToPush = ReadPackageFromRequest())
            {
                if (packageToPush.Metadata.MinClientVersion > new Version("3.0.0.0"))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UploadPackage_MinClientVersionOutOfRange,
                        packageToPush.Metadata.MinClientVersion));
                }

                // Ensure that the user can push packages for this partialId.
                var packageRegistration = PackageService.FindPackageRegistrationById(packageToPush.Metadata.Id);
                if (packageRegistration != null)
                {
                    if (!packageRegistration.IsOwner(user))
                    {
                        return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                    }

                    // Check if a particular Id-Version combination already exists. We eventually need to remove this check.
                    string normalizedVersion = packageToPush.Metadata.Version.ToNormalizedString();
                    bool packageExists =
                        packageRegistration.Packages.Any(
                            p => String.Equals(
                                p.NormalizedVersion,
                                normalizedVersion,
                                StringComparison.OrdinalIgnoreCase));

                    if (packageExists)
                    {
                        return new HttpStatusCodeWithBodyResult(
                            HttpStatusCode.Conflict,
                            String.Format(CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified,
                                          packageToPush.Metadata.Id, packageToPush.Metadata.Version.ToNormalizedStringSafe()));
                    }
                }

                var package = PackageService.CreatePackage(packageToPush, user, commitChanges: false);
                AutoCuratePackage.Execute(package, packageToPush, commitChanges: false);
                EntitiesContext.SaveChanges();

                using (Stream uploadStream = packageToPush.GetStream())
                {
                    await PackageFileService.SavePackageFileAsync(package, uploadStream);
                    IndexingService.UpdatePackage(package);
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.Created);
        }

        [HttpDelete]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("DeletePackageApi")]
        public virtual ActionResult DeletePackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            var user = GetCurrentUser();
            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
            }

            PackageService.MarkPackageUnlisted(package);
            IndexingService.UpdatePackage(package);
            return new EmptyResult();
        }

        [HttpPost]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("PublishPackageApi")]
        public virtual ActionResult PublishPackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            User user = GetCurrentUser();
            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));
            }

            PackageService.MarkPackageListed(package);
            IndexingService.UpdatePackage(package);
            return new EmptyResult();
        }

        [OutputCache(Duration = 30, Location = OutputCacheLocation.ServerAndClient, VaryByParam = "*")]
        public virtual async Task<ActionResult> ServiceAlert()
        {
            var markdownContentFileExtension = NuGetGallery.ContentService.MarkdownContentFileExtension;
            string alertString = null;
            var alert = await ContentService.GetContentItemAsync(Constants.ContentNames.Alert, markdownContentFileExtension, TimeSpan.Zero);
            if (alert != null)
            {
                alertString = alert.ToString().Replace("</div>", " - Check our <a href=\"http://status.nuget.org\">status page</a> for updates.</div>");
            }

            if (String.IsNullOrEmpty(alertString) && _config.ReadOnlyMode)
            {
                var readOnly = await ContentService.GetContentItemAsync(Constants.ContentNames.ReadOnly, markdownContentFileExtension, TimeSpan.Zero);
                alertString = (readOnly == null) ? null : readOnly.ToString();
            }
            return Content(alertString, "text/html");
        }

        public virtual async Task<ActionResult> Team()
        {
            var team = await ContentService.GetContentItemAsync(Constants.ContentNames.Team, TimeSpan.FromHours(1));
            return Content(team.ToString(), "application/json");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            var exception = filterContext.Exception;
            if (exception is ReadOnlyModeException)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.ServiceUnavailable, exception.Message);
            }
            else
            {
                var request = filterContext.HttpContext.Request;
                filterContext.ExceptionHandled = true;
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.InternalServerError, exception.Message, request.IsLocal ? exception.StackTrace : exception.Message);
            }
        }

        protected internal virtual INupkg ReadPackageFromRequest()
        {
            Stream stream;
            if (Request.Files.Count > 0)
            {
                // If we're using the newer API, the package stream is sent as a file.
                stream = Request.Files[0].InputStream;
            }
            else
            {
                stream = Request.InputStream;
            }

            return new Nupkg(stream, leaveOpen: false);
        }

        [HttpGet]
        [ActionName("PackageIDs")]
        public virtual async Task<ActionResult> GetPackageIds(string partialId, bool? includePrerelease)
        {
            var query = GetService<IPackageIdsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(partialId, includePrerelease)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("PackageVersions")]
        public virtual async Task<ActionResult> GetPackageVersions(string id, bool? includePrerelease)
        {
            var query = GetService<IPackageVersionsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(id, includePrerelease)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("StatisticsDownloadsApi")]
        public virtual async Task<ActionResult> GetStatsDownloads(int? count)
        {
            var result = await StatisticsService.LoadDownloadPackageVersions();

            if (result.Loaded)
            {
                int i = 0;

                JArray content = new JArray();
                foreach (StatisticsPackagesItemViewModel row in StatisticsService.DownloadPackageVersionsAll)
                {
                    JObject item = new JObject();

                    item.Add("PackageId", row.PackageId);
                    item.Add("PackageVersion", row.PackageVersion);
                    item.Add("Gallery", Url.PackageGallery(row.PackageId, row.PackageVersion));
                    item.Add("PackageTitle", row.PackageTitle ?? row.PackageId);
                    item.Add("PackageDescription", row.PackageDescription);
                    item.Add("PackageIconUrl", row.PackageIconUrl ?? Url.PackageDefaultIcon());
                    item.Add("Downloads", row.Downloads);

                    content.Add(item);

                    i++;

                    if (count.HasValue && count.Value == i)
                    {
                        break;
                    }
                }

                return new ContentResult
                {
                    Content = content.ToString(),
                    ContentType = "application/json"
                };
            }

            return new HttpStatusCodeResult(HttpStatusCode.NotFound);
        }
    }
}
