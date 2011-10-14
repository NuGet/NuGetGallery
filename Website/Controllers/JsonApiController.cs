using System.Web;
using System.Web.Mvc;
using System.Linq;
using MvcHaack.Ajax;

namespace NuGetGallery
{
    public partial class JsonApiController : JsonController
    {
        IPackageService packageSvc;
        IUserService userSvc;

        public JsonApiController(IPackageService packageSvc, IUserService userSvc)
        {
            this.packageSvc = packageSvc;
            this.userSvc = userSvc;
        }

        [Authorize]
        public virtual object GetPackageOwners(string id, string version)
        {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new { message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var owners = from u in package.PackageRegistration.Owners
                         select new OwnerModel { name = u.Username, current = u.Username == HttpContext.User.Identity.Name }; ;

            return owners;
        }

        public object AddPackageOwner(string id, string version, string username)
        {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new { success = false, message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = userSvc.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found" };
            }

            packageSvc.AddPackageOwner(package, user);
            return new { success = true, name = user.Username };
        }

        public object RemovePackageOwner(string id, string version, string username)
        {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new { success = false, message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = userSvc.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found" };
            }

            packageSvc.RemovePackageOwner(package, user);
            return new { success = true };
        }

        public class OwnerModel
        {
            public string name { get; set; }
            public bool current { get; set; }
        }
    }
}
