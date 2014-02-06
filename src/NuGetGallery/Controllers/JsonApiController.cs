using System.Linq;
using System.Web.Mvc;
using MvcHaack.Ajax;
using System.Globalization;

namespace NuGetGallery
{
    public partial class JsonApiController : JsonController
    {
        private readonly IMessageService _messageService;
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IMessageService messageService)
        {
            _packageService = packageService;
            _userService = userService;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _messageService = messageService;
        }

        [Authorize]
        public virtual object GetPackageOwners(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
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
            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return new { success = false, message = "Package not found." };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found." };
            }
            if (!user.Confirmed)
            {
                return new { success = false, message = string.Format(CultureInfo.InvariantCulture, "Sorry, {0} hasn't verified their email account yet and we cannot proceed with the request.", username) };
            }

            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            var ownerRequest = _packageService.CreatePackageOwnerRequest(package, currentUser, user);

            var confirmationUrl = Url.ConfirmationUrl(
                "ConfirmOwner", 
                "Packages",
                user.Username,
                ownerRequest.ConfirmationCode,
                new { id = package.Id });
            _messageService.SendPackageOwnerRequest(currentUser, user, package, confirmationUrl);

            return new { success = true, name = user.Username, pending = true };
        }

        public object RemovePackageOwner(string id, string username)
        {
            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return new { success = false, message = "Package not found" };
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new { success = false, message = "You are not the package owner." };
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return new { success = false, message = "Owner not found" };
            }

            _packageService.RemovePackageOwner(package, user);
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