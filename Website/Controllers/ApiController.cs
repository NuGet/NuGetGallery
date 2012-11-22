using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using NuGet;

namespace NuGetGallery
{
    public partial class ApiController : AppController
    {
        private readonly INuGetExeDownloaderService _nugetExeDownloaderSvc;
        private readonly IPackageFileService _packageFileSvc;
        private readonly IPackageService _packageSvc;
        private readonly IUserService _userSvc;

        public ApiController(
            IPackageService packageSvc,
            IPackageFileService packageFileSvc,
            IUserService userSvc,
            INuGetExeDownloaderService nugetExeDownloaderSvc)
        {
            _packageSvc = packageSvc;
            _packageFileSvc = packageFileSvc;
            _userSvc = userSvc;
            _nugetExeDownloaderSvc = nugetExeDownloaderSvc;
        }

        [ActionName("GetPackageApi")]
        [HttpGet]
        public virtual async Task<ActionResult> GetPackage(string id, string version)
        {
            // if the version is null, the user is asking for the latest version. Presumably they don't want includePrerelease release versions. 
            // The allow prerelease flag is ignored if both partialId and version are specified.
            var package = _packageSvc.FindPackageByIdAndVersion(id, version, allowPrerelease: false);
            
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            _packageSvc.AddDownloadStatistics(
                package,
                Request.UserHostAddress,
                Request.UserAgent);

            if (!String.IsNullOrWhiteSpace(package.ExternalPackageUrl))
            {
                return Redirect(package.ExternalPackageUrl);
            }
            else
            {
                return await _packageFileSvc.CreateDownloadPackageActionResultAsync(package);
            }
        }

        [ActionName("GetNuGetExeApi")]
        [HttpGet]
        [OutputCache(VaryByParam = "none", Location = OutputCacheLocation.ServerAndClient, Duration = 600)]
        public virtual Task<ActionResult> GetNuGetExe()
        {
            return _nugetExeDownloaderSvc.CreateNuGetExeDownloadActionResultAsync();
        }

        [ActionName("VerifyPackageKeyApi")]
        [HttpGet]
        public virtual ActionResult VerifyPackageKey(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userSvc.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
            }

            if (!String.IsNullOrEmpty(id))
            {
                // If the partialId is present, then verify that the user has permission to push for the specific Id \ version combination.
                var package = _packageSvc.FindPackageByIdAndVersion(id, version);
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

        [ActionName("PushPackageApi")]
        [HttpPut]
        public virtual Task<ActionResult> CreatePackagePut(string apiKey)
        {
            return CreatePackageInternal(apiKey);
        }

        [ActionName("PushPackageApi")]
        [HttpPost]
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

            var user = _userSvc.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
            }

            var packageToPush = ReadPackageFromRequest();

            // Ensure that the user can push packages for this partialId.
            var packageRegistration = _packageSvc.FindPackageRegistrationById(packageToPush.Id);
            if (packageRegistration != null)
            {
                if (!packageRegistration.IsOwner(user))
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
                }

                // Check if a particular Id-Version combination already exists. We eventually need to remove this check.
                bool packageExists =
                    packageRegistration.Packages.Any(p => p.Version.Equals(packageToPush.Version.ToString(), StringComparison.OrdinalIgnoreCase));
                if (packageExists)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.Conflict,
                        String.Format(CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, packageToPush.Id, packageToPush.Version));
                }
            }

            var package = await _packageSvc.CreatePackageAsync(packageToPush, user);
            if (packageToPush.Id.Equals(Constants.NuGetCommandLinePackageId, StringComparison.OrdinalIgnoreCase) && package.IsLatestStable)
            {
                // If we're pushing a new stable version of NuGet.CommandLine, update the extracted executable.
                await _nugetExeDownloaderSvc.UpdateExecutableAsync(packageToPush);
            }

            return new HttpStatusCodeResult(201);
        }

        [ActionName("DeletePackageApi")]
        [HttpDelete]
        public virtual ActionResult DeletePackage(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userSvc.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "delete"));
            }

            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
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

            _packageSvc.MarkPackageUnlisted(package);
            return new EmptyResult();
        }

        [ActionName("PublishPackageApi")]
        [HttpPost]
        public virtual ActionResult PublishPackage(string apiKey, string id, string version)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKey));
            }

            var user = _userSvc.FindByApiKey(parsedApiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));
            }

            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
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

            _packageSvc.MarkPackageListed(package);
            return new EmptyResult();
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            filterContext.ExceptionHandled = true;
            var exception = filterContext.Exception;
            var request = filterContext.HttpContext.Request;
            filterContext.Result = new HttpStatusCodeWithBodyResult(
                HttpStatusCode.InternalServerError, exception.Message, request.IsLocal ? exception.StackTrace : exception.Message);
        }

        protected internal virtual IPackage ReadPackageFromRequest()
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

            return new ZipPackage(stream);
        }

        [ActionName("PackageIDs")]
        [HttpGet]
        public virtual ActionResult GetPackageIds(
            string partialId,
            bool? includePrerelease)
        {
            var qry = GetService<IPackageIdsQuery>();
            return new JsonNetResult(qry.Execute(partialId, includePrerelease).ToArray());
        }

        [ActionName("PackageVersions")]
        [HttpGet]
        public virtual ActionResult GetPackageVersions(
            string id,
            bool? includePrerelease)
        {
            var qry = GetService<IPackageVersionsQuery>();
            return new JsonNetResult(qry.Execute(id, includePrerelease).ToArray());
        }
    }
}