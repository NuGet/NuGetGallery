using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery {
    public partial class PackagesController : Controller {
        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        readonly ICryptographyService cryptoSvc;
        readonly IPackageService packageSvc;
        readonly IPackageFileService packageFileSvc;
        readonly IUserService userSvc;
        readonly IMessageService messageService;

        public PackagesController(
            ICryptographyService cryptoSvc,
            IPackageService packageSvc,
            IPackageFileService packageFileRepo,
            IUserService userSvc,
            IMessageService messageService) {
            this.cryptoSvc = cryptoSvc;
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileRepo;
            this.userSvc = userSvc;
            this.messageService = messageService;
        }

        [Authorize]
        public virtual ActionResult UploadPackage() {
            return View();
        }

        [Authorize, HttpPost]
        public virtual ActionResult UploadPackage(HttpPostedFileBase packageFile) {
            // TODO: validate package id and version don't already exist

            if (packageFile == null) {
                ModelState.AddModelError(string.Empty, "A package file is required.");
                return View();
            }

            // TODO: what other security checks do we need to perform for uploaded packages?
            var extension = Path.GetExtension(packageFile.FileName).ToLowerInvariant();
            if (extension != Const.PackageFileExtension) {
                ModelState.AddModelError(string.Empty, "The package file must be a .nupkg file.");
                return View();
            }

            var currentUser = userSvc.FindByUsername(User.Identity.Name);
            if (currentUser == null) {
                throw new InvalidOperationException("Current user is null. This should never happen!");
            }

            ZipPackage uploadedPackage;
            using (var uploadStream = packageFile.InputStream) {
                uploadedPackage = new ZipPackage(packageFile.InputStream);
            }

            Package packageVersion;
            try {
                packageVersion = packageSvc.CreatePackage(uploadedPackage, currentUser);
            }
            catch (EntityException ex) {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View();
            }

            string packagePublishUrl = Url.Publish(packageVersion);
            return Redirect(packagePublishUrl);
        }

        [ActionName("PublishPackage"), Authorize]
        public virtual ActionResult ShowPublishPackageForm(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            return View(new SubmitPackageViewModel {
                Id = package.PackageRegistration.Id,
                Version = package.Version,
                Title = package.Title,
                Summary = package.Summary,
                Description = package.Description,
                RequiresLicenseAcceptance = package.RequiresLicenseAcceptance,
                LicenseUrl = package.LicenseUrl,
                Tags = package.Tags,
                ProjectUrl = package.ProjectUrl,
                Authors = package.FlattenedAuthors,
            });
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public virtual ActionResult PublishPackage(string id, string version) {
            // TODO: handle requesting to verify a package that is already verified; return 404?
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            packageSvc.PublishPackage(package.PackageRegistration.Id, package.Version);

            // TODO: add a flash success message

            return Redirect(Url.Package(package));
        }

        public virtual ActionResult DisplayPackage(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null) {
                return PackageNotFound(id, version);
            }
            var model = new DisplayPackageViewModel(package);
            return View(model);
        }

        public virtual ActionResult ListPackages(string q, string sortOrder = Const.DefaultPackageListSortOrder, int page = 1) {
            if (page < 1) {
                page = 1;
            }

            IQueryable<Package> packageVersions = null;
            if (String.IsNullOrEmpty(q)) {
                packageVersions = packageSvc.GetLatestVersionOfPublishedPackages();
            }
            else {
                packageVersions = packageSvc.GetLatestVersionOfPublishedPackages().Search(q);
            }

            var viewModel = new PackageListViewModel(packageVersions,
                q,
                sortOrder,
                page - 1,
                Const.DefaultPackageListPageSize,
                Url);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        // NOTE: Intentionally NOT requiring authentication
        public virtual ActionResult ReportAbuse(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null) {
                return PackageNotFound(id, version);
            }

            var model = new ReportAbuseViewModel {
                PackageId = id,
                PackageVersion = package.Version
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public virtual ActionResult ReportAbuse(string id, string version, ReportAbuseViewModel reportForm) {
            if (!ModelState.IsValid) {
                return ReportAbuse(id, version);
            }
            var package = packageSvc.FindPackageByIdAndVersion(id);
            if (package == null) {
                return PackageNotFound(id, version);
            }

            if (Request.IsAuthenticated) {
                var user = userSvc.FindByUsername(HttpContext.User.Identity.Name);
                reportForm.Email = user.EmailAddress;
                // TODO: Add more details before we send the message.
            }
            // TODO: Email!

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";
            return Redirect(Url.Package(id));
        }

        [Authorize]
        public virtual ActionResult ContactOwners(string id) {
            var package = packageSvc.FindPackageRegistrationById(id);

            if (package == null) {
                return PackageNotFound(id);
            }

            var model = new ContactOwnersViewModel {
                PackageId = package.Id,
                Owners = package.Owners
            };

            return View(model);
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public virtual ActionResult ContactOwners(string id, ContactOwnersViewModel contactForm) {
            if (!ModelState.IsValid) {
                return ContactOwners(id);
            }

            var package = packageSvc.FindPackageRegistrationById(id);
            if (package == null) {
                return PackageNotFound(id);
            }

            var user = userSvc.FindByUsername(HttpContext.User.Identity.Name);
            var fromAddress = new MailAddress(user.EmailAddress, user.Username);
            messageService.SendContactOwnersMessage(fromAddress, package, contactForm.Message);

            string message = String.Format("Your message has been sent to the owners of {0}.", id);
            TempData["Message"] = message;
            return Redirect(Url.Package(id));
        }

        public virtual ActionResult DownloadPackage(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null) {
                return PackageNotFound(id, version);
            }

            return packageFileSvc.CreateDownloadPackageResult(package);
        }

        [Authorize]
        public virtual ActionResult ManagePackageOwners(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new ManagePackageOwnersViewModel(package, HttpContext.User);

            return View(model);
        }

        [Authorize]
        public virtual ActionResult Delete(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var dependents = packageSvc.FindDependentPackages(package);
            var model = new DeletePackageViewModel(package, dependents);

            return View(model);
        }

        [ActionName("Delete"), Authorize, HttpPost, ValidateAntiForgeryToken]
        public virtual ActionResult DeletePackage(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var dependents = packageSvc.FindDependentPackages(package);
            var model = new DeletePackageViewModel(package, dependents);

            if (!model.MayDelete) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            packageSvc.DeletePackage(id, version);

            TempData["Message"] = "The package was deleted";

            return Redirect(Url.PackageList());
        }

        [Authorize]
        public virtual ActionResult Edit(string id, string version) {
            return GetPackageOwnerActionFormResult(id, version);
        }

        private ActionResult GetPackageOwnerActionFormResult(string id, string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null) {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User)) {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DisplayPackageViewModel(package);
            return View(model);
        }

        // We may want to have a specific behavior for package not found
        private ActionResult PackageNotFound(string id) {
            return PackageNotFound(id, null);
        }

        private ActionResult PackageNotFound(string id, string version) {
            return HttpNotFound();
        }
    }
}
