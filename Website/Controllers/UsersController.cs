using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery {
    public class UsersController : Controller {
        public const string Name = "Users";

        readonly IFormsAuthenticationService formsAuthSvc;
        readonly IUserService userService;
        readonly IPackageService packageService;

        public UsersController(
            IFormsAuthenticationService formsAuthSvc,
            IUserService userSvc,
            IPackageService packageService) {
            this.formsAuthSvc = formsAuthSvc;
            this.userService = userSvc;
            this.packageService = packageService;
        }

        [Authorize]
        public ActionResult Account() {
            var user = userService.FindByUsername(HttpContext.User.Identity.Name);
            return View(user);
        }

        public ActionResult Register() {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterRequest request) {
            // TODO: consider client-side validation for unique username
            // TODO: add email validation

            if (!ModelState.IsValid)
                return View();

            User user;
            try {
                user = userService.Create(
                    request.Username,
                    request.Password,
                    request.EmailAddress);
            }
            catch (EntityException ex) {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View();
            }

            formsAuthSvc.SetAuthCookie(
                user.Username,
                true);

            return RedirectToRoute(RouteName.Home);
        }

        [Authorize]
        public ActionResult Packages() {
            var user = userService.FindByUsername(HttpContext.User.Identity.Name);
            var packages = packageService.FindPackagesByOwner(user);

            var published = from p in packages
                            where p.Published != null
                            group p by p.PackageRegistration.Id;

            var model = new ManagePackagesViewModel {
                PublishedPackages = from pr in published
                                    select new PackageViewModel(pr.First()) {
                                        DownloadCount = pr.Sum(p => p.DownloadCount),
                                        Version = null
                                    },
                UnpublishedPackages = from p in packages
                                      where p.Published == null
                                      select new PackageViewModel(p)
            };
            return View(model);
        }

        [Authorize]
        public ActionResult GenerateApiKey() {
            return Redirect(Request.Url.ToString());
        }

        public ActionResult ForgotPassword() {
            return View();
        }
    }
}
