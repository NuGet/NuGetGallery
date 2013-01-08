﻿using System;
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
        private readonly IConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly INuGetExeDownloaderService _nugetExeDownloaderService;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly ISearchService _searchService;
        private readonly IUploadFileService _uploadFileService;
        private readonly IUserService _userService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IIndexingService _indexingService;

        public PackagesController(
            IPackageService packageService,
            IUploadFileService uploadFileService,
            IUserService userService,
            IMessageService messageService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratedPackageCmd,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IConfiguration config,
            IIndexingService indexingService)
        {
            _packageService = packageService;
            _uploadFileService = uploadFileService;
            _userService = userService;
            _messageService = messageService;
            _searchService = searchService;
            _autoCuratedPackageCmd = autoCuratedPackageCmd;
            _nugetExeDownloaderService = nugetExeDownloaderService;
            _packageFileService = packageFileService;
            _entitiesContext = entitiesContext;
            _config = config;
            _indexingService = indexingService;
        }

        [Authorize]
        public async virtual Task<ActionResult> UploadPackage()
        {
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
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
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
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

            var packageRegistration = _packageService.FindPackageRegistrationById(nuGetPackage.Id);
            if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
            {
                ModelState.AddModelError(
                    String.Empty, String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                return View();
            }

            var package = _packageService.FindPackageByIdAndVersion(nuGetPackage.Id, nuGetPackage.Version.ToStringSafe());
            if (package != null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    String.Format(
                        CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                return View();
            }

            using (Stream stream = nuGetPackage.GetStream())
            {
                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, stream);
            }

            return RedirectToRoute(RouteName.VerifyPackage);
        }

        public virtual ActionResult DisplayPackage(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
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

            IQueryable<Package> packageVersions = _packageService.GetPackagesForListing(prerelease);

            q = (q ?? "").Trim();

            if (String.IsNullOrEmpty(sortOrder))
            {
                // Determine the default sort order. If no query string is specified, then the sortOrder is DownloadCount
                // If we are searching for something, sort by relevance.
                sortOrder = q.IsEmpty() ? Constants.PopularitySortOrder : Constants.RelevanceSortOrder;
            }

            var searchFilter = GetSearchFilter(q, sortOrder, page, prerelease);
            int totalHits;
            packageVersions = _searchService.Search(packageVersions, searchFilter, out totalHits);
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
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new ReportAbuseViewModel
                {
                    PackageId = id,
                    PackageVersion = package.Version,
                };

            if (Request.IsAuthenticated)
            {
                var user = _userService.FindByUsername(HttpContext.User.Identity.Name);
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

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            MailAddress from;
            if (Request.IsAuthenticated)
            {
                var user = _userService.FindByUsername(HttpContext.User.Identity.Name);
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
            var package = _packageService.FindPackageRegistrationById(id);

            if (package == null)
            {
                return HttpNotFound();
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

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = _userService.FindByUsername(HttpContext.User.Identity.Name);
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
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
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
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            if (!(listed ?? false))
            {
                _packageService.MarkPackageUnlisted(package);
            }
            else
            {
                _packageService.MarkPackageListed(package);
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

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = _userService.FindByUsername(username);
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
                    Success = _packageService.ConfirmPackageOwner(package, user, token),
                    PackageId = id
                };

            return View(model);
        }

        internal virtual ActionResult Edit(string id, string version, bool? listed, Func<Package, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            if (!(listed ?? false))
            {
                _packageService.MarkPackageUnlisted(package);
            }
            else
            {
                _packageService.MarkPackageListed(package);
            }
            return Redirect(urlFactory(package));
        }

        private ActionResult GetPackageOwnerActionFormResult(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DisplayPackageViewModel(package);
            return View(model);
        }

        [Authorize]
        public virtual async Task<ActionResult> VerifyPackage()
        {
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            IPackage package;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
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
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            IPackage nugetPackage;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return HttpNotFound();
                }

                nugetPackage = CreatePackage(uploadFile);
            }

            // update relevant database tables
            Package package = _packageService.CreatePackage(nugetPackage, currentUser, commitChanges: false);
            _packageService.PublishPackage(package, commitChanges: false);

            if (listed == false)
            {
                _packageService.MarkPackageUnlisted(package, commitChanges: false);
            }
            _autoCuratedPackageCmd.Execute(package, nugetPackage);

            // commit all changes to database as an atomic transaction
            _entitiesContext.SaveChanges();

            // save package to blob storage
            using (Stream stream = nugetPackage.GetStream())
            {
                await _packageFileService.SavePackageFileAsync(package, stream);
            }

            // tell Lucene to update index for the new package
            _indexingService.UpdateIndex();

            // delete the uploaded binary in the Uploads container
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            // If we're pushing a new stable version of NuGet.CommandLine, update the extracted executable.
            if (package.PackageRegistration.Id.Equals(Constants.NuGetCommandLinePackageId, StringComparison.OrdinalIgnoreCase) &&
                package.IsLatestStable)
            {
                await _nugetExeDownloaderService.UpdateExecutableAsync(nugetPackage);
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
            var currentUser = _userService.FindByUsername(GetIdentity().Name);
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

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

        private static SearchFilter GetSearchFilter(string q, string sortOrder, int page, bool includePrerelease)
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