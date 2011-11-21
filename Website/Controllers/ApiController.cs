using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public partial class ApiController : Controller
    {
        private readonly IPackageService packageSvc;
        private readonly IUserService userSvc;
        private readonly IPackageFileService packageFileSvc;

        public ApiController(IPackageService packageSvc, IPackageFileService packageFileSvc, IUserService userSvc)
        {
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileSvc;
            this.userSvc = userSvc;
        }

        [ActionName("GetPackageApi"), HttpGet]
        public virtual ActionResult GetPackage(string id, string version)
        {
            // if the version is null, the user is asking for the latest version. Presumably they don't want pre release versions.
            var package = packageSvc.FindPackageByIdAndVersion(id, version, allowPrerelease: false);

            if (package == null)
                return new HttpNotFoundResult(string.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
    
            packageSvc.AddDownloadStatistics(package,
                                             Request.UserHostAddress,
                                             Request.UserAgent);

            return packageFileSvc.CreateDownloadPackageActionResult(package);
        }

        [ActionName("PushPackageApi"), HttpPut, RequireRemoteHttps]
        public virtual ActionResult CreatePackage(Guid apiKey)
        {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                return new HttpStatusCodeResult((int)HttpStatusCode.Forbidden, string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));

            var packageToPush = ReadPackageFromRequest();

            var package = packageSvc.FindPackageByIdAndVersion(packageToPush.Id, packageToPush.Version.ToString());
            if (package != null)
                return new HttpStatusCodeResult((int)HttpStatusCode.Conflict, string.Format(Strings.PackageExistsAndCannotBeModified, packageToPush.Id, packageToPush.Version.ToString()));

            package = packageSvc.CreatePackage(packageToPush, user);
            return new HttpStatusCodeResult(201);
        }

        [ActionName("DeletePackageApi"), HttpDelete, RequireRemoteHttps]
        public virtual ActionResult DeletePackage(Guid apiKey, string id, string version)
        {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                return new HttpStatusCodeResult((int)HttpStatusCode.Forbidden, string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "delete"));

            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
                return new HttpNotFoundResult(string.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));

            if (!package.IsOwner(user))
                return new HttpStatusCodeResult((int)HttpStatusCode.Forbidden, string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "delete"));

            packageSvc.MarkPackageUnlisted(package);
            return new EmptyResult();
        }

        [ActionName("PublishPackageApi"), HttpPost, RequireRemoteHttps]
        public virtual ActionResult PublishPackage(Guid key, string id, string version)
        {
            var user = userSvc.FindByApiKey(key);
            if (user == null)
                return new HttpStatusCodeResult((int)HttpStatusCode.Forbidden, string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));

            packageSvc.PublishPackage(id, version);
            return new EmptyResult();
        }

        public virtual IPackage ReadPackageFromRequest()
        {
            Stream stream;
            if (Request.Files.Count > 0)
            {
                // If we're using the newer API, the package stream is sent as a file.
                stream = Request.Files[0].InputStream;
            }
            else
                stream = Request.InputStream;

            return new ZipPackage(stream);
        }
    }
}