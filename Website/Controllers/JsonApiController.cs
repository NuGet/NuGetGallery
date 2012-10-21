using System.Linq;
using System.Web.Mvc;
using MvcHaack.Ajax;

namespace NuGetGallery
{
    public partial class JsonApiController : JsonController
    {
        private readonly IMessageService _messageSvc;
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;
        private readonly IPackageService _packageSvc;
        private readonly IUserService _userSvc;

        public JsonApiController(
            IPackageService packageSvc,
            IUserService userSvc,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IMessageService messageService)
        {
            _packageSvc = packageSvc;
            _userSvc = userSvc;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _messageSvc = messageService;
        }

        [Authorize]
        public virtual object GetPackageOwners(string id, string version)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new { message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var owners = from u in package.PackageRegistration.Owners
                         select new OwnerModel
                                    {
                                        name = u.Username,
                                        current = u.Username == HttpContext.User.Identity.Name,
                                        pending = false
                                    };

            var pending = from u in _packageOwnerRequestRepository.GetAll()
                          where u.PackageRegistrationKey == package.PackageRegistration.Key
                          select new OwnerModel { name = u.NewOwner.Username, current = false, pending = true };

            return owners.Union(pending);
        }

        public object AddPackageOwner(string id, string username)
        {
            var package = _packageSvc.FindPackageRegistrationById(id);
            if (package == null)
            {
                return new { success = false, message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = _userSvc.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found" };
            }

            var currentUser = _userSvc.FindByUsername(HttpContext.User.Identity.Name);
            var ownerRequest = _packageSvc.CreatePackageOwnerRequest(package, currentUser, user);

            var confirmationUrl = Url.ConfirmationUrl(
                MVC.Packages.ConfirmOwner().AddRouteValue("id", package.Id), user.Username, ownerRequest.ConfirmationCode, Request.Url.Scheme);
            _messageSvc.SendPackageOwnerRequest(currentUser, user, package, confirmationUrl);

            return new { success = true, name = user.Username, pending = true };
        }

        public object RemovePackageOwner(string id, string username)
        {
            var package = _packageSvc.FindPackageRegistrationById(id);
            if (package == null)
            {
                return new { success = false, message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = _userSvc.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found" };
            }

            _packageSvc.RemovePackageOwner(package, user);
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