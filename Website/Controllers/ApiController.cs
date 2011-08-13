using System;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery {
    public class ApiController : Controller {
        public static string Name = "Api";

        readonly IPackageService packageSvc;
        readonly IUserService userSvc;

        public ApiController(IPackageService packageSvc, IUserService userSvc) {
            this.packageSvc = packageSvc;
            this.userSvc = userSvc;
        }

        [ActionName("PushPackageApi"), HttpPost]
        public ActionResult CreatePackage(Guid apiKey) {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                throw new Exception("The specified API key does not provide the authority to push packages.");

            var packageToPush = new ZipPackage(Request.InputStream);

            // TODO: allow package replacement via this API
            var package = packageSvc.FindPackageByIdAndVersion(packageToPush.Id, packageToPush.Version.ToString());
            if (package != null)
                throw new Exception(string.Format("A package with id '{0}' and version '{1}' already exists and cannot be modified.", packageToPush.Id, packageToPush.Version.ToString()));

            package = packageSvc.CreatePackage(packageToPush, user);
            return new EmptyResult();
        }

        [ActionName("DeletePackageApi"), HttpDelete]
        public ActionResult DeletePackage(Guid apiKey, string id, string version) {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                throw new Exception("The specified API key does not provide the authority to push packages.");

            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
                throw new Exception(string.Format("A package with id '{0}' and version '{1}' does not exist.", id, version));

            packageSvc.DeletePackage(id, version);
            return new EmptyResult();
        }

        [ActionName("PublishPackageApi"), HttpPost]
        public ActionResult PublishPackage(Guid key, string id, string version) {
            var user = userSvc.FindByApiKey(key);
            if (user == null)
                throw new Exception("The specified API key does not provide the authority to push packages.");

            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
                throw new Exception(string.Format("A package with id '{0}' and version '{1}' does not exist.", id, version));

            packageSvc.PublishPackage(id, version);
            return new EmptyResult();
        }
    }
}