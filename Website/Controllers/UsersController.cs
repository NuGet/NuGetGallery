using System;
using System.Linq;
using System.Net.Mail;
using System.Web.Mvc;

namespace NuGetGallery {
    public partial class UsersController : Controller {
        readonly IFormsAuthenticationService formsAuthSvc;
        readonly IUserService userService;
        readonly IPackageService packageService;
        readonly IMessageService messageService;

        public UsersController(
            IFormsAuthenticationService formsAuthSvc,
            IUserService userSvc,
            IPackageService packageService,
            IMessageService messageService) {
            this.formsAuthSvc = formsAuthSvc;
            this.userService = userSvc;
            this.packageService = packageService;
            this.messageService = messageService;
        }

        [Authorize]
        public virtual ActionResult Account() {
            var user = userService.FindByUsername(HttpContext.User.Identity.Name);
            return View(user);
        }

        public virtual ActionResult Register() {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public virtual ActionResult Register(RegisterRequest request) {
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

            // Passing in scheme to force fully qualified URL
            var confirmationUrl = Url.Action(MVC.Users.Confirm().AddRouteValue("token", user.ConfirmationToken), protocol: Request.Url.Scheme);

            messageService.SendNewAccountEmail(new MailAddress(user.EmailAddress), confirmationUrl);
            return RedirectToRoute(MVC.Users.Thanks());
        }

        public virtual ActionResult Thanks() {
            return View();
        }

        [Authorize]
        public virtual ActionResult Packages() {
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

        [Authorize, ValidateAntiForgeryToken, HttpPost]
        public virtual ActionResult GenerateApiKey() {
            userService.GenerateApiKey(HttpContext.User.Identity.Name);
            return RedirectToAction(MVC.Users.Account());
        }

        public virtual ActionResult ForgotPassword() {
            return View();
        }

        public virtual ActionResult Confirm(string token) {
            bool? confirmed = null;
            if (!String.IsNullOrEmpty(token)) {
                confirmed = userService.ConfirmAccount(token);
            }
            ViewBag.Confirmed = confirmed;
            return View();
        }

        public virtual ActionResult Profiles(string username) {
            var user = userService.FindByUsername(username);
            if (user == null) {
                return HttpNotFound();
            }

            var packages = (from p in packageService.FindPackagesByOwner(user)
                            select new PackageViewModel(p)).ToList();

            var model = new UserProfileModel {
                Username = user.Username,
                EmailAddress = user.EmailAddress,
                Packages = packages,
                TotalPackageDownloadCount = packages.Sum(p => p.TotalDownloadCount)
            };

            return View(model);
        }
    }
}
