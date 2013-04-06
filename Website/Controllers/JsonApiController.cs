using System.Linq;
using System.Web.Mvc;
using MvcHaack.Ajax;

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

        [Authorize]
        public object IsFavorite(string id)
        {
            string username = HttpContext.User.Identity.Name;
            var result = _userService.IsFollowing(username, id);
            return new { success = true, favorite = result };
        }

        [Authorize]
        public object WhereIsFavorite(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return new { success = true, favorites = new string[0] };
            }

            string username = HttpContext.User.Identity.Name;
            string[] idArray = ids.Split('|');

            var result = _userService.GetFollowedPackageIdsInSet(username, idArray);
            return new { success = true, favorites = result };
        }

        [Authorize]
        [HttpPost]
        public object FavoritePackage(string id)
        {
            string username = HttpContext.User.Identity.Name;
            _userService.Follow(username, id, saveChanges: true);
            return new { success = true };
        }

        [Authorize]
        [HttpPost]
        public object UnfavoritePackage(string id)
        {
            string username = HttpContext.User.Identity.Name;
            _userService.Unfollow(username, id, saveChanges: true);
            return new { success = true };
        }

        [Authorize]
        public object AddPackageOwner(string id, string username)
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

            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            var ownerRequest = _packageService.CreatePackageOwnerRequest(package, currentUser, user);

            var confirmationUrl = Url.ConfirmationUrl(
                MVC.Packages.ConfirmOwner().AddRouteValue("id", package.Id), user.Username, ownerRequest.ConfirmationCode, Request.Url.Scheme);
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

        protected override void OnException(ExceptionContext filterContext)
        {
            if (!filterContext.ExceptionHandled)
            {
                if (filterContext.Exception is UserNotFoundException)
                {
                    filterContext.ExceptionHandled = true;
                    filterContext.Result =
                        new JsonResult
                        {
                            Data = new { success = false, message = "User not found" },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                }
            }
            base.OnException(filterContext);
        }

        public class OwnerModel
        {
            public string name { get; set; }
            public bool current { get; set; }
            public bool pending { get; set; }
        }
    }
}