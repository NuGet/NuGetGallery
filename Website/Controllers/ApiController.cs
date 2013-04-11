using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using NuGet;

namespace NuGetGallery
{
    public partial class ApiController : AppController
    {
        private readonly INuGetExeDownloaderService _nugetExeDownloaderService;
        private readonly IPackageFileService _packageFileService;
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IStatisticsService _statisticsService;

        public ApiController(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService)
        {
            _packageService = packageService;
            _packageFileService = packageFileService;
            _userService = userService;
            _nugetExeDownloaderService = nugetExeDownloaderService;
            _statisticsService = null;
        }

        public ApiController(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IStatisticsService statisticsService)
        {
            _packageService = packageService;
            _packageFileService = packageFileService;
            _userService = userService;
            _nugetExeDownloaderService = nugetExeDownloaderService;
            _statisticsService = statisticsService;
        }

        [ActionName("GetPackageApi")]
        [HttpGet]
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

            // if the version is null, the user is asking for the latest version. Presumably they don't want includePrerelease release versions. 
            // The allow prerelease flag is ignored if both partialId and version are specified.
            // In general we want to try to add download statistics for any package regardless of whether a version was specified.
            try
            {
                Package package = _packageService.FindPackageByIdAndVersion(id, version, allowPrerelease: false);
                if (package == null)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                }

                try
                {
                    _packageService.AddDownloadStatistics(
                        package,
                        Request.UserHostAddress,
                        Request.UserAgent,
                        Request.Headers["NuGet-Operation"]);
                }
                catch (ReadOnlyModeException)
                {
                    // *gulp* Swallowed. It's OK not to add statistics and ok to not log errors in read only mode.
                }
                catch (SqlException e)
                {
                    // Log the error and continue
                    QuietlyLogException(e);
                }
                catch (DataException e)
                {
                    // Log the error and continue
                    QuietlyLogException(e);
                }

                return await _packageFileService.CreateDownloadPackageActionResultAsync(HttpContext.Request.Url, package);
            }
            catch (SqlException e)
            {
                QuietlyLogException(e);
            }
            catch (DataException e)
            {
                QuietlyLogException(e);
            }

            // Fall back to constructing the URL based on the package version and ID.

            return await _packageFileService.CreateDownloadPackageActionResultAsync(HttpContext.Request.Url, id, version);
        }

        [HttpGet]
        [ActionName("GetNuGetExeApi")]
        [OutputCache(VaryByParam = "none", Location = OutputCacheLocation.ServerAndClient, Duration = 600)]
        public virtual Task<ActionResult> GetNuGetExe()
        {
            return _nugetExeDownloaderService.CreateNuGetExeDownloadActionResultAsync(HttpContext.Request.Url);
        }

        [HttpGet]
        [ActionName("VerifyPackageKeyApi")]
        public virtual ActionResult VerifyPackageKey(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userService.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
            }

            if (!String.IsNullOrEmpty(id))
            {
                // If the partialId is present, then verify that the user has permission to push for the specific Id \ version combination.
                var package = _packageService.FindPackageByIdAndVersion(id, version);
                if (package == null)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                }

                if (!package.IsOwner(user))
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
                }
            }

            return new EmptyResult();
        }

        [HttpPut]
        [ActionName("PushPackageApi")]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual Task<ActionResult> CreatePackagePut(string apiKey)
        {
            return CreatePackageInternal(apiKey);
        }

        [HttpPost]
        [ActionName("PushPackageApi")]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual Task<ActionResult> CreatePackagePost(string apiKey)
        {
            return CreatePackageInternal(apiKey);
        }

        private async Task<ActionResult> CreatePackageInternal(string apiKey)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userService.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
            }

            using (var packageToPush = ReadPackageFromRequest())
            {
                // Ensure that the user can push packages for this partialId.
                var packageRegistration = _packageService.FindPackageRegistrationById(packageToPush.Metadata.Id);
                if (packageRegistration != null)
                {
                    if (!packageRegistration.IsOwner(user))
                    {
                        return new HttpStatusCodeWithBodyResult(
                            HttpStatusCode.Forbidden,
                            String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
                    }

                    // Check if a particular Id-Version combination already exists. We eventually need to remove this check.
                    bool packageExists =
                        packageRegistration.Packages.Any(
                            p =>
                            p.Version.Equals(packageToPush.Metadata.Version.ToString(),
                                             StringComparison.OrdinalIgnoreCase));
                    if (packageExists)
                    {
                        return new HttpStatusCodeWithBodyResult(
                            HttpStatusCode.Conflict,
                            String.Format(CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified,
                                          packageToPush.Metadata.Id, packageToPush.Metadata.Version));
                    }
                }

                var package = _packageService.CreatePackage(packageToPush, user, commitChanges: true);
                using (Stream uploadStream = packageToPush.GetStream())
                {
                    await _packageFileService.SavePackageFileAsync(package, uploadStream);
                }

                if (
                    packageToPush.Metadata.Id.Equals(Constants.NuGetCommandLinePackageId,
                                                     StringComparison.OrdinalIgnoreCase) && package.IsLatestStable)
                {
                    // If we're pushing a new stable version of NuGet.CommandLine, update the extracted executable.
                    await _nugetExeDownloaderService.UpdateExecutableAsync(packageToPush);
                }
            }

            return new HttpStatusCodeResult(201);
        }

        [HttpDelete]
        [ActionName("DeletePackageApi")]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult DeletePackage(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userService.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "delete"));
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "delete"));
            }

            _packageService.MarkPackageUnlisted(package);
            return new EmptyResult();
        }

        [HttpPost]
        [ActionName("PublishPackageApi")]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult PublishPackage(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userService.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));
            }

            _packageService.MarkPackageListed(package);
            return new EmptyResult();
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

        [ActionName("PackageIDs")]
        [HttpGet]
        public virtual ActionResult GetPackageIds(string partialId, bool? includePrerelease)
        {
            var query = GetService<IPackageIdsQuery>();
            return new JsonResult
                {
                    Data = (query.Execute(partialId, includePrerelease).ToArray()),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
        }

        [ActionName("PackageVersions")]
        [HttpGet]
        public virtual ActionResult GetPackageVersions(string id, bool? includePrerelease)
        {
            var query = GetService<IPackageVersionsQuery>();
            return new JsonResult
                {
                    Data = query.Execute(id, includePrerelease).ToArray(),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
        }

        [ActionName("StatisticsDownloadsApi")]
        [HttpGet]
        public virtual async Task<ActionResult> GetStatsDownloads(int? count)
        {
            if (_statisticsService != null)
            {
                bool isAvailable = await _statisticsService.LoadDownloadPackageVersions();

                if (isAvailable)
                {
                    int i = 0;

                    JArray content = new JArray();
                    foreach (StatisticsPackagesItemViewModel row in _statisticsService.DownloadPackageVersionsAll)
                    {
                        JObject item = new JObject();

                        item.Add("PackageId", row.PackageId);
                        item.Add("PackageVersion", row.PackageVersion);
                        item.Add("Gallery", Url.PackageGallery(row.PackageId, row.PackageVersion));
                        item.Add("Package", Url.PackageDownload(2, row.PackageId, row.PackageVersion));
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
            }

            return new HttpStatusCodeResult(HttpStatusCode.NotFound);
        }

        private static void QuietlyLogException(Exception e)
        {
            try
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(e);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}
