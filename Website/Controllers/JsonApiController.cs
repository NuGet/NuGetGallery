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
        IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository;

        public JsonApiController(IPackageService packageSvc, IUserService userSvc, IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository)
        {
            this.packageSvc = packageSvc;
            this.userSvc = userSvc;
            this.packageOwnerRequestRepository = packageOwnerRequestRepository;
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
                         select new OwnerModel {
                             name = u.Username,
                             current = u.Username == HttpContext.User.Identity.Name,
                             pending = false
                         };

            var pending = from u in packageOwnerRequestRepository.GetAll()
                          where u.PackageRegistrationKey == package.PackageRegistration.Key
                          select new OwnerModel { name = u.NewOwner.Username, current = false, pending = true };

            return owners.Union(pending);
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

            var currentUser = userSvc.FindByUsername(HttpContext.User.Identity.Name);

            packageSvc.RequestPackageOwner(package.PackageRegistration, currentUser, user);
            return new { success = true, name = user.Username, pending = true };
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
            public bool pending { get; set; }
        }
    }
}
