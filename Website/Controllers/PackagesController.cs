using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using NuGet;
using PoliteCaptcha;

namespace NuGetGallery
{
    public partial class PackagesController : Controller
    {
        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        private readonly IAutomaticallyCuratePackageCommand _autoCuratedPackageCmd;
        private readonly ICacheService _cacheSvc;
        private readonly IConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly INuGetExeDownloaderService _nugetExeDownloaderSvc;
        private readonly IPackageService _packageSvc;
        private readonly IPackageFileService _packageFileSvc;
        private readonly ISearchService _searchSvc;
        private readonly IUploadFileService _uploadFileSvc;
        private readonly IUserService _userSvc;

        public PackagesController(
            IPackageService packageSvc,
            IUploadFileService uploadFileSvc,
            IUserService userSvc,
            IMessageService messageService,
            ISearchService searchSvc,
            ICacheService cacheSvc,
            IAutomaticallyCuratePackageCommand autoCuratedPackageCmd,
            INuGetExeDownloaderService nugetExeDownloaderSvc,
            IConfiguration config,
            IPackageFileService packageFileSvc)
        {
            _packageSvc = packageSvc;
            _uploadFileSvc = uploadFileSvc;
            _userSvc = userSvc;
            _messageService = messageService;
            _searchSvc = searchSvc;
            _autoCuratedPackageCmd = autoCuratedPackageCmd;
            _nugetExeDownloaderSvc = nugetExeDownloaderSvc;
            _packageFileSvc = packageFileSvc;
            _cacheSvc = cacheSvc;
            _config = config;
        }

        [Authorize]
        public async virtual Task<ActionResult> UploadPackage()
        {
            var currentUser = _userSvc.FindByUsername(GetIdentity().Name);

            using (var existingUploadFile = await _uploadFileSvc.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return RedirectToRoute(RouteName.VerifyPackage);
                }
            }

            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UploadPackage(HttpPostedFileBase uploadFile)
        {
            var currentUser = _userSvc.FindByUsername(GetIdentity().Name);

            using (var existingUploadFile = await _uploadFileSvc.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return new HttpStatusCodeResult(409, "Cannot upload file because an upload is already in progress.");
                }
            }

            if (uploadFile == null)
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileIsRequired);
                return View();
            }

            if (!Path.GetExtension(uploadFile.FileName).Equals(Constants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileMustBeNuGetPackage);
                return View();
            }

            IPackage nuGetPackage;
            try
            {
                using (var uploadStream = uploadFile.InputStream)
                {
                    nuGetPackage = CreatePackage(uploadStream);
                }
            }
            catch
            {
                ModelState.AddModelError(String.Empty, Strings.FailedToReadUploadFile);
                return View();
            }

            var packageRegistration = _packageSvc.FindPackageRegistrationById(nuGetPackage.Id);
            if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
            {
                ModelState.AddModelError(
                    String.Empty, String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                return View();
            }

            var package = _packageSvc.FindPackageByIdAndVersion(nuGetPackage.Id, nuGetPackage.Version.ToStringSafe());
            if (package != null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    String.Format(
                        CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                return View();
            }

            using (var fileStream = nuGetPackage.GetStream())
            {
                await _uploadFileSvc.SaveUploadFileAsync(currentUser.Key, fileStream);
            }

            return RedirectToRoute(RouteName.VerifyPackage);
        }

        public virtual ActionResult DisplayPackage(string id, string version)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return PackageNotFound(id, version);
            }
            var model = new DisplayPackageViewModel(package);
            ViewBag.FacebookAppID = _config.FacebookAppID;
            return View(model);
        }

        public virtual ActionResult ListPackages(string q, string sortOrder = null, int page = 1, bool prerelease = false)
        {
            if (page < 1)
            {
                page = 1;
            }

            IQueryable<Package> packageVersions = _packageSvc.GetPackagesForListing(prerelease);

            q = (q ?? "").Trim();

            if (String.IsNullOrEmpty(sortOrder))
            {
                // Determine the default sort order. If no query string is specified, then the sortOrder is DownloadCount
                // If we are searching for something, sort by relevance.
                sortOrder = q.IsEmpty() ? Constants.PopularitySortOrder : Constants.RelevanceSortOrder;
            }

            var searchFilter = GetSearchFilter(q, sortOrder, page, prerelease);
            int totalHits;
            packageVersions = _searchSvc.Search(packageVersions, searchFilter, out totalHits);
            if (page == 1 && !packageVersions.Any())
            {
                // In the event the index wasn't updated, we may get an incorrect count. 
                totalHits = 0;
            }

            var viewModel = new PackageListViewModel(
                packageVersions,
                q,
                sortOrder,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url,
                prerelease);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        // NOTE: Intentionally NOT requiring authentication
        public virtual ActionResult ReportAbuse(string id, string version)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return PackageNotFound(id, version);
            }

            var model = new ReportAbuseViewModel
                {
                    PackageId = id,
                    PackageVersion = package.Version,
                };

            if (Request.IsAuthenticated)
            {
                var user = _userSvc.FindByUsername(HttpContext.User.Identity.Name);
                if (user.Confirmed)
                {
                    model.ConfirmedUser = true;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual ActionResult ReportAbuse(string id, string version, ReportAbuseViewModel reportForm)
        {
            if (!ModelState.IsValid)
            {
                return ReportAbuse(id, version);
            }

            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return PackageNotFound(id, version);
            }

            MailAddress from;
            if (Request.IsAuthenticated)
            {
                var user = _userSvc.FindByUsername(HttpContext.User.Identity.Name);
                from = user.ToMailAddress();
            }
            else
            {
                from = new MailAddress(reportForm.Email);
            }

            _messageService.ReportAbuse(from, package, reportForm.Message);

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";
            return RedirectToAction(MVC.Packages.DisplayPackage(id, version));
        }

        [Authorize]
        public virtual ActionResult ContactOwners(string id)
        {
            var package = _packageSvc.FindPackageRegistrationById(id);

            if (package == null)
            {
                return PackageNotFound(id);
            }

            var model = new ContactOwnersViewModel
            {
                PackageId = package.Id,
                Owners = package.Owners.Where(u => u.EmailAllowed)
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ContactOwners(string id, ContactOwnersViewModel contactForm)
        {
            if (!ModelState.IsValid)
            {
                return ContactOwners(id);
            }

            var package = _packageSvc.FindPackageRegistrationById(id);
            if (package == null)
            {
                return PackageNotFound(id);
            }

            var user = _userSvc.FindByUsername(HttpContext.User.Identity.Name);
            var fromAddress = new MailAddress(user.EmailAddress, user.Username);
            _messageService.SendContactOwnersMessage(
                fromAddress, package, contactForm.Message, Url.Action(MVC.Users.Edit(), protocol: Request.Url.Scheme));

            string message = String.Format(CultureInfo.CurrentCulture, "Your message has been sent to the owners of {0}.", id);
            TempData["Message"] = message;
            return RedirectToAction(MVC.Packages.DisplayPackage(id, null));
        }

        // This is the page that explains why there's no download link.
        public virtual ActionResult Download()
        {
            return View();
        }

        [Authorize]
        public virtual ActionResult ManagePackageOwners(string id, string version)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new ManagePackageOwnersViewModel(package, HttpContext.User);

            return View(model);
        }

        [Authorize]
        public virtual ActionResult Delete(string id, string version)
        {
            return GetPackageOwnerActionFormResult(id, version);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Delete(string id, string version, bool? listed)
        {
            return Delete(id, version, listed, Url.Package);
        }

        internal virtual ActionResult Delete(string id, string version, bool? listed, Func<Package, string> urlFactory)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            if (!(listed ?? false))
            {
                _packageSvc.MarkPackageUnlisted(package);
            }
            else
            {
                _packageSvc.MarkPackageListed(package);
            }

            return Redirect(urlFactory(package));
        }

        [Authorize]
        public virtual ActionResult Edit(string id, string version)
        {
            return GetPackageOwnerActionFormResult(id, version);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Edit(string id, string version, bool? listed)
        {
            return Edit(id, version, listed, Url.Package);
        }

        [Authorize]
        public virtual ActionResult ConfirmOwner(string id, string username, string token)
        {
            if (String.IsNullOrEmpty(token))
            {
                return HttpNotFound();
            }

            var package = _packageSvc.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = _userSvc.FindByUsername(username);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (!String.Equals(user.Username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpStatusCodeResult(403);
            }

            var model = new PackageOwnerConfirmationModel
                {
                    Success = _packageSvc.ConfirmPackageOwner(package, user, token),
                    PackageId = id
                };

            return View(model);
        }

        internal virtual ActionResult Edit(string id, string version, bool? listed, Func<Package, string> urlFactory)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            if (!(listed ?? false))
            {
                _packageSvc.MarkPackageUnlisted(package);
            }
            else
            {
                _packageSvc.MarkPackageListed(package);
            }
            return Redirect(urlFactory(package));
        }

        private ActionResult GetPackageOwnerActionFormResult(string id, string version)
        {
            var package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return PackageNotFound(id, version);
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DisplayPackageViewModel(package);
            return View(model);
        }

        // We may want to have a specific behavior for package not found
        private ActionResult PackageNotFound(string id)
        {
            return PackageNotFound(id, version: null);
        }

        private ActionResult PackageNotFound(string id, string version)
        {
            return HttpNotFound();
        }

        [Authorize]
        public virtual async Task<ActionResult> VerifyPackage()
        {
            var currentUser = _userSvc.FindByUsername(GetIdentity().Name);

            IPackage package;
            using (Stream uploadFile = await _uploadFileSvc.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return RedirectToRoute(RouteName.UploadPackage);
                }

                package = CreatePackage(uploadFile);
            }

            return View(
                new VerifyPackageViewModel
                    {
                        Id = package.Id,
                        Version = package.Version.ToStringSafe(),
                        Title = package.Title,
                        Summary = package.Summary,
                        Description = package.Description,
                        RequiresLicenseAcceptance = package.RequireLicenseAcceptance,
                        LicenseUrl = package.LicenseUrl.ToStringSafe(),
                        Tags = package.Tags,
                        ProjectUrl = package.ProjectUrl.ToStringSafe(),
                        Authors = package.Authors.Flatten(),
                        Listed = package.Listed
                    });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> VerifyPackage(bool? listed)
        {
            var currentUser = _userSvc.FindByUsername(GetIdentity().Name);

            IPackage nugetPackage;
            using (Stream uploadFile = await _uploadFileSvc.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return HttpNotFound();
                }

                nugetPackage = CreatePackage(uploadFile);
            }

            Package package;
            using (var tx = new TransactionScope())
            {
                package = await _packageSvc.CreatePackageAsync(nugetPackage, currentUser);
                _packageSvc.PublishPackage(package.PackageRegistration.Id, package.Version);
                if (listed.HasValue && listed.Value == false)
                {
                    _packageSvc.MarkPackageUnlisted(package);
                }
                await _uploadFileSvc.DeleteUploadFileAsync(currentUser.Key);
                _autoCuratedPackageCmd.Execute(package, nugetPackage);
                tx.Complete();
            }

            if (package.PackageRegistration.Id.Equals(Constants.NuGetCommandLinePackageId, StringComparison.OrdinalIgnoreCase) &&
                package.IsLatestStable)
            {
                // If we're pushing a new stable version of NuGet.CommandLine, update the extracted executable.
                await _nugetExeDownloaderSvc.UpdateExecutableAsync(nugetPackage);
            }

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture, Strings.SuccessfullyUploadedPackage, package.PackageRegistration.Id, package.Version);

            return RedirectToRoute(RouteName.DisplayPackage, new { package.PackageRegistration.Id, package.Version });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> CancelUpload()
        {
            var currentUser = _userSvc.FindByUsername(GetIdentity().Name);
            await _uploadFileSvc.DeleteUploadFileAsync(currentUser.Key);

            return RedirectToAction("UploadPackage");
        }

        // this methods exist to make unit testing easier
        protected internal virtual IIdentity GetIdentity()
        {
            return User.Identity;
        }

        // this methods exist to make unit testing easier
        protected internal virtual IPackage CreatePackage(Stream stream)
        {
            return new ZipPackage(stream);
        }

        private SearchFilter GetSearchFilter(string q, string sortOrder, int page, bool includePrerelease)
        {
            var searchFilter = new SearchFilter
            {
                SearchTerm = q,
                Skip = (page - 1) * Constants.DefaultPackageListPageSize, // pages are 1-based. 
                Take = Constants.DefaultPackageListPageSize,
                IncludePrerelease = includePrerelease
            };

            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    searchFilter.SortProperty = SortProperty.DisplayName;
                    searchFilter.SortDirection = SortDirection.Ascending;
                    break;

                case Constants.RecentSortOrder:
                    searchFilter.SortProperty = SortProperty.Recent;
                    break;

                case Constants.PopularitySortOrder:
                    searchFilter.SortProperty = SortProperty.DownloadCount;
                    break;

                default:
                    searchFilter.SortProperty = SortProperty.Relevance;
                    break;
            }
            return searchFilter;
        }

        private static string GetSortExpression(string sortOrder)
        {
            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    return "PackageRegistration.Id";

                case Constants.RecentSortOrder:
                    return "Published desc";

                default:
                    return "PackageRegistration.DownloadCount desc";
            }
        }		
    }
}