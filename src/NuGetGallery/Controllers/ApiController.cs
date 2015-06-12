// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using NuGet;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Packaging;
using NuGetGallery.Configuration;
using System.Text;
using System.Net.Http;

namespace NuGetGallery
{
    public partial class ApiController : AppController
    {
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
        
        protected ApiController() { }

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

            if (!String.IsNullOrEmpty(version))
            {
                SemanticVersion dummy;
                if (!SemanticVersion.TryParse(version, out dummy))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The package version is not a valid semantic version");
                }
            }

            // Normalize the version
            version = SemanticVersionExtensions.Normalize(version);

            // if the version is null, the user is asking for the latest version. Presumably they don't want includePrerelease release versions. 
            // The allow prerelease flag is ignored if both partialId and version are specified.
            // In general we want to try to add download statistics for any package regardless of whether a version was specified.

            // Only allow database requests to take up to 5 seconds.  Any longer and we just lose the data; oh well.
            EntitiesContext.SetCommandTimeout(5);

            Package package = null;
            try
            {
                package = PackageService.FindPackageByIdAndVersion(id, version, allowPrerelease: false);
                if (package == null)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                }

                try
                {
                    var stats = new PackageStatistics
                    {
                        IPAddress = Request.UserHostAddress,
                        UserAgent = Request.UserAgent,
                        Package = package,
                        Operation = Request.Headers["NuGet-Operation"],
                        DependentPackage = Request.Headers["NuGet-DependentPackage"],
                        ProjectGuids = Request.Headers["NuGet-ProjectGuids"],
                    };

                    if (_config == null || _config.MetricsServiceUri == null)
                    {
                        PackageService.AddDownloadStatistics(stats);
                    }
                    else
                    {
                        // Disable warning about not awaiting async calls because we are _intentionally_ not awaiting this.
#pragma warning disable 4014
                        Task.Run(() => PostDownloadStatistics(id, package.NormalizedVersion, stats.IPAddress, stats.UserAgent, stats.Operation, stats.DependentPackage, stats.ProjectGuids));
#pragma warning restore 4014
                    }
                }
                catch (ReadOnlyModeException)
                {
                    // *gulp* Swallowed. It's OK not to add statistics and ok to not log errors in read only mode.
                }
                catch (SqlException e)
                {
                    // Log the error and continue
                    QuietLog.LogHandledException(e);
                }
                catch (DataException e)
                {
                    // Log the error and continue
                    QuietLog.LogHandledException(e);
                }
            }
            catch (SqlException e)
            {
                QuietLog.LogHandledException(e);
            }
            catch (DataException e)
            {
                QuietLog.LogHandledException(e);
            }
            
            // Fall back to constructing the URL based on the package version and ID.
            if (String.IsNullOrEmpty(version) && package == null)
            {
                // Database was unavailable and we don't have a version, return a 503
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
            }
            return await PackageFileService.CreateDownloadPackageActionResultAsync(
                HttpContext.Request.Url, 
                id, 
                String.IsNullOrEmpty(version) ? package.NormalizedVersion : version);
        }

        private const string IdKey = "id";
        private const string VersionKey = "version";
        private const string IPAddressKey = "ipAddress";
        private const string UserAgentKey = "userAgent";
        private const string OperationKey = "operation";
        private const string DependentPackageKey = "dependentPackage";
        private const string ProjectGuidsKey = "projectGuids";
        private const string HTTPPost = "POST";
        private const string MetricsDownloadEventMethod = "/DownloadEvent";
        private const string ContentTypeJson = "application/json";

        private static JObject GetJObject(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = new JObject();
            jObject.Add(IdKey, id);
            jObject.Add(VersionKey, version);
            if (!String.IsNullOrEmpty(ipAddress)) jObject.Add(IPAddressKey, ipAddress);
            if (!String.IsNullOrEmpty(userAgent)) jObject.Add(UserAgentKey, userAgent);
            if (!String.IsNullOrEmpty(operation)) jObject.Add(OperationKey, operation);
            if (!String.IsNullOrEmpty(dependentPackage)) jObject.Add(DependentPackageKey, dependentPackage);
            if (!String.IsNullOrEmpty(projectGuids)) jObject.Add(ProjectGuidsKey, projectGuids);

            return jObject;
        }

        private async Task PostDownloadStatistics(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            if (_config == null || _config.MetricsServiceUri == null)
                return;

            try
            {
                var jObject = GetJObject(id, version, ipAddress, userAgent, operation, dependentPackage, projectGuids);

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    await httpClient.PostAsync(new Uri(_config.MetricsServiceUri, MetricsDownloadEventMethod), new StringContent(jObject.ToString(), Encoding.UTF8, ContentTypeJson));
                }
            }
            catch (WebException ex)
            {
                QuietLog.LogHandledException(ex);
            }
            catch(AggregateException ex)
            {
                QuietLog.LogHandledException(ex.InnerException ?? ex);
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
        [ActionName("VerifyPackageKeyApi")]
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
                if (packageToPush.Metadata.MinClientVersion > typeof(Manifest).Assembly.GetName().Version)
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

        public virtual async Task<ActionResult> ServiceAlert()
        {
            string alertString = null;
            var alert = await ContentService.GetContentItemAsync(Constants.ContentNames.Alert, TimeSpan.Zero);
            if (alert != null)
            {
                alertString = alert.ToString().Replace("</div>", " - Check our <a href=\"http://status.nuget.org\">status page</a> for updates.</div>");
            }
            
            if (String.IsNullOrEmpty(alertString) && _config.ReadOnlyMode)
            {
                var readOnly = await ContentService.GetContentItemAsync(Constants.ContentNames.ReadOnly, TimeSpan.Zero);
                alertString = (readOnly == null) ? (string)null : readOnly.ToString();
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
        public virtual ActionResult GetPackageIds(string partialId, bool? includePrerelease)
        {
            var query = GetService<IPackageIdsQuery>();
            return new JsonResult
                {
                    Data = (query.Execute(partialId, includePrerelease).ToArray()),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
        }

        [HttpGet]
        [ActionName("PackageVersions")]
        public virtual ActionResult GetPackageVersions(string id, bool? includePrerelease)
        {
            var query = GetService<IPackageVersionsQuery>();
            return new JsonResult
                {
                    Data = query.Execute(id, includePrerelease).ToArray(),
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
