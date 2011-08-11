using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery {
    public class PackagesController : Controller {
        public const string Name = "Packages";

        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        readonly ICryptographyService cryptoSvc;
        readonly IPackageService packageSvc;
        readonly IPackageFileService packageFileSvc;
        readonly IUserService userSvc;

        public PackagesController(
            ICryptographyService cryptoSvc,
            IPackageService packageSvc,
            IPackageFileService packageFileRepo,
            IUserService userSvc) {
            this.cryptoSvc = cryptoSvc;
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileRepo;
            this.userSvc = userSvc;
        }

        [ActionName(ActionName.SubmitPackage), Authorize]
        public ActionResult ShowSubmitPackageForm() {
            return View();
        }

        [ActionName(ActionName.SubmitPackage), Authorize, HttpPost]
        public ActionResult SubmitPackage(HttpPostedFileBase packageFile) {
            // TODO: validate package id and version don't already exist

            if (packageFile == null) {
                ModelState.AddModelError(string.Empty, "A package file is required.");
                return View();
            }

            // TODO: what other security checks do we need to perform for uploaded packages?
            var extension = Path.GetExtension(packageFile.FileName).ToLowerInvariant();
            if (extension != Const.PackageExtension) {
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

            return RedirectToRoute(
                RouteName.PublishPackage,
                new { id = packageVersion.PackageRegistration.Id, version = packageVersion.Version });
        }

        [ActionName(ActionName.PublishPackage), Authorize]
        public ActionResult ShowPublishPackageForm(
            string id,
            string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null)
                return HttpNotFound();

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

        [ActionName(ActionName.PublishPackage), Authorize, HttpPost]
        public ActionResult PublishPackage(
            string id,
            string version) {
            // TODO: handle requesting to verify a package that is already verified; return 404?

            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null)
                return HttpNotFound();

            packageSvc.PublishPackage(package.PackageRegistration.Id, package.Version);

            // TODO: add a flash success message

            return RedirectToRoute(RouteName.DisplayPackage, new { id = package.PackageRegistration.Id, version = package.Version });
        }

        public ActionResult DisplayPackage(
            string id,
            string version) {
            var package = packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null)
                return HttpNotFound();

            return View(new DisplayPackageViewModel(package) {
                IconUrl = package.IconUrl ?? Url.Content("~/Content/Images/packagesDefaultIcon.png"),
            });
        }

        [ActionName(ActionName.ListPackages)]
        public ActionResult ListPackages() {
            var packageVersions = packageSvc.GetLatestVersionOfPublishedPackages();

            var viewModel = packageVersions.Select(pv =>
                new ListPackageViewModel {
                    Id = pv.PackageRegistration.Id,
                    Version = pv.Version,
                });

            return View(viewModel);
        }

        //TODO: Implement the get and post for this
        public ActionResult ReportAbuse() {
            return View();
        }

        //TODO: Implement the get and post for this
        public ActionResult ContactOwners() {
            return View();
        }
    }
}
