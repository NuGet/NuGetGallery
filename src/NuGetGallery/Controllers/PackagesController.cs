using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;
using PoliteCaptcha;

namespace NuGetGallery
{
    public partial class PackagesController : Controller
    {
        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        private readonly IAutomaticallyCuratePackageCommand _autoCuratedPackageCmd;
        private readonly IAppConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly INuGetExeDownloaderService _nugetExeDownloaderService;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly ISearchService _searchService;
        private readonly IUploadFileService _uploadFileService;
        private readonly IUserService _userService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IIndexingService _indexingService;
        private readonly ICacheService _cacheService;
        private readonly EditPackageService _editPackageService;

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
            IAppConfiguration config,
            IIndexingService indexingService,
            ICacheService cacheService,
            EditPackageService editPackageService)
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
            _cacheService = cacheService;
            _editPackageService = editPackageService;
        }

        [Authorize]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public virtual ActionResult UploadPackageProgress()
        {
            string username = GetIdentity().Name;

            AsyncFileUploadProgress progress = _cacheService.GetProgress(username);
            if (progress == null)
            {
                return HttpNotFound();
            }
            return Json(progress, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult UndoPendingEdits(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new TrivialPackageVersionModel
            {
                Id = package.PackageRegistration.Id,
                Version = package.Version,
                Title = package.Title,
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("UndoPendingEdits")]
        public virtual ActionResult UndoPendingEditsPost(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            // To do as much successful cancellation as possible, Will not batch, but will instead try to cancel 
            // pending edits 1 at a time, starting with oldest first.
            var pendingEdits = _entitiesContext.Set<PackageEdit>()
                .Where(pe => pe.PackageKey == package.Key)
                .OrderBy(pe => pe.Timestamp)
                .ToList();

            int numOK = 0;
            int numConflicts = 0;
            foreach (var result in pendingEdits)
            {
                try
                {
                    _entitiesContext.DeleteOnCommit(result);
                    _entitiesContext.SaveChanges();
                    numOK += 1;
                }
                catch (DataException)
                {
                    numConflicts += 1;
                }
            }

            if (numConflicts > 0)
            {
                TempData["Message"] = "Your pending edit has already been completed and could not be canceled.";
            }
            else if (numOK > 0)
            {
                TempData["Message"] = "Your pending edits for this package were successfully canceled.";
            }
            else
            {
                TempData["Message"] = "No pending edits were found for this package. The edits may have already been completed.";
            }

            return Redirect(Url.Package(id, version));
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

            using (var uploadStream = uploadFile.InputStream)
            {
                INupkg nuGetPackage;
                try
                {
                    nuGetPackage = CreatePackage(uploadStream);
                }
                catch
                {
                    ModelState.AddModelError(String.Empty, Strings.FailedToReadUploadFile);
                    return View();
                }
                finally
                {
                    _cacheService.RemoveProgress(currentUser.Username);
                }

                var packageRegistration = _packageService.FindPackageRegistrationById(nuGetPackage.Metadata.Id);
                if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
                {
                    ModelState.AddModelError(
                        String.Empty, String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                    return View();
                }

                var package = _packageService.FindPackageByIdAndVersion(nuGetPackage.Metadata.Id, nuGetPackage.Metadata.Version.ToStringSafe());
                if (package != null)
                {
                    ModelState.AddModelError(
                        String.Empty,
                        String.Format(
                            CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                    return View();
                }

                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, nuGetPackage.GetStream());
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

            if (package.IsOwner(HttpContext.User))
            {
                // Tell logged-in package owners not to cache the package page, so they won't be confused about the state of pending edits.
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                Response.Cache.SetMaxAge(TimeSpan.Zero);
                Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

                var pendingMetadata = _editPackageService.GetPendingMetadata(package);
                if (pendingMetadata != null)
                {
                    model.SetPendingMetadata(pendingMetadata);
                }
            }

            ViewBag.FacebookAppID = _config.FacebookAppId;
            return View(model);
        }

        public virtual ActionResult ListPackages(string q, string sortOrder = null, int page = 1, bool prerelease = false)
        {
            if (page < 1)
            {
                page = 1;
            }

            q = (q ?? "").Trim();

            if (String.IsNullOrEmpty(sortOrder))
            {
                // Determine the default sort order. If no query string is specified, then the sortOrder is DownloadCount
                // If we are searching for something, sort by relevance.
                sortOrder = q.IsEmpty() ? Constants.PopularitySortOrder : Constants.RelevanceSortOrder;
            }

            var searchFilter = SearchAdaptor.GetSearchFilter(q, sortOrder, page, prerelease);
            int totalHits;
            IQueryable<Package> packageVersions = _searchService.Search(searchFilter, out totalHits);
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
        private static readonly ReportPackageReason[] ReportOtherPackageReasons = new[] {
            ReportPackageReason.IsFraudulent,
            ReportPackageReason.ViolatesALicenseIOwn,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.HasABug,
            ReportPackageReason.Other
        };
        public virtual ActionResult ReportAbuse(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportOtherPackageReasons,
                PackageId = id,
                PackageVersion = package.Version,
            };

            if (Request.IsAuthenticated)
            {
                var user = _userService.FindByUsername(HttpContext.User.Identity.Name);

                // If user logged on in as owner a different tab, then clicked the link, we can redirect them to ReportMyPackage
                if (package.IsOwner(user))
                {
                    return RedirectToAction(ActionNames.ReportMyPackage, new {id, version});
                }

                if (user.Confirmed)
                {
                    model.ConfirmedUser = true;
                }
            }

            ViewData[Constants.ReturnUrlViewDataKey] = Url.Action(ActionNames.ReportMyPackage, new {id, version});
            return View(model);
        }

        private static readonly ReportPackageReason[] ReportMyPackageReasons = new[] {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.PublishedWithWrongVersion,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };
        [Authorize]
        public virtual ActionResult ReportMyPackage(string id, string version)
        {
            var user = _userService.FindByUsername(HttpContext.User.Identity.Name);

            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            // If user hit this url by constructing it manually but is not the owner, redirect them to ReportAbuse
            if (!(HttpContext.User.IsInRole(Constants.AdminRoleName) || package.IsOwner(user)))
            {
                return RedirectToAction(ActionNames.ReportAbuse, new { id, version });
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportMyPackageReasons,
                ConfirmedUser = user.Confirmed,
                PackageId = id,
                PackageVersion = package.Version,
            };

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

            User user = null;
            MailAddress from;
            if (Request.IsAuthenticated)
            {
                user = _userService.FindByUsername(HttpContext.User.Identity.Name);
                from = user.ToMailAddress();
            }
            else
            {
                from = new MailAddress(reportForm.Email);
            }

            var request = new ReportPackageRequest
            {
                AlreadyContactedOwners = reportForm.AlreadyContactedOwner,
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                Url = Url
            };
            _messageService.ReportAbuse(request
                );

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual ActionResult ReportMyPackage(string id, string version, ReportAbuseViewModel reportForm)
        {
            if (!ModelState.IsValid)
            {
                return ReportMyPackage(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = _userService.FindByUsername(HttpContext.User.Identity.Name);
            MailAddress from = user.ToMailAddress();

            _messageService.ReportMyPackage(
                new ReportPackageRequest
                {
                    FromAddress = from,
                    Message = reportForm.Message,
                    Package = package,
                    Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                    RequestingUser = user,
                    Url = Url
                });

            TempData["Message"] = "Your support request has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
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
            // Edit does exactly the same thing that Delete used to do... REUSE ALL THE CODE!
            return Edit(id, version, listed, Url.Package);
        }

        [Authorize]
        public virtual ActionResult Edit(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            var packageRegistration = _packageService.FindPackageRegistrationById(id);
            var model = new EditPackageRequest
            {
                PackageId = package.PackageRegistration.Id,
                PackageTitle = package.Title,
                Version = package.Version,
                PackageVersions = packageRegistration.Packages
                    .OrderByDescending(p => new SemanticVersion(p.Version), Comparer<SemanticVersion>.Create((a, b) => a.CompareTo(b)))
                    .ToList(),
            };

            var pendingMetadata = _editPackageService.GetPendingMetadata(package);
            model.HasPendingMetadata = pendingMetadata != null;
            model.Edit = new EditPackageVersionRequest(package, pendingMetadata);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Edit(string id, string version, EditPackageRequest formData, string returnUrl)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = _userService.FindByUsername(HttpContext.User.Identity.Name);
            if (user == null || !package.IsOwner(HttpContext.User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // Add the edit request to a queue where it will be processed in the background.
            if (formData.Edit != null)
            {
                _editPackageService.StartEditPackageRequest(package, formData.Edit, user);
                _entitiesContext.SaveChanges();
            }

            return Redirect(RedirectHelper.SafeRedirectUrl(Url, returnUrl ?? Url.Package(id, version)));
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

            ConfirmOwnershipResult result;
            if (User.IsAdministrator())
            {
                result = ConfirmOwnershipResult.AlreadyOwner;
            }
            else
            {
                var user = _userService.FindByUsername(username);
                if (user == null)
                {
                    return HttpNotFound();
                }

                if (!String.Equals(user.Username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpStatusCodeResult(403);
                }

                result = _packageService.ConfirmPackageOwner(package, user, token);
            }

            var model = new PackageOwnerConfirmationModel
                {
                    Result = result,
                    PackageId = package.Id
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

            string action;
            if (!(listed ?? false))
            {
                action = "unlisted";
                _packageService.MarkPackageUnlisted(package);
            }
            else
            {
                action = "listed";
                _packageService.MarkPackageListed(package);
            }
            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture,
                "The package has been {0}. It may take several hours for this change to propagate through our system.", 
                action);

            // Update the index
            _indexingService.UpdatePackage(package);
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

            IPackageMetadata packageMetadata;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return RedirectToRoute(RouteName.UploadPackage);
                }

                try
                {
                    using (INupkg package = CreatePackage(uploadFile))
                    {
                        packageMetadata = package.Metadata;
                    }
                }
                catch (InvalidDataException e)
                {
                    // Log the exception in case we get support requests about it.
                    QuietLog.LogHandledException(e);

                    return View(Views.UnverifiablePackage);
                }
            }

            return View(
                new VerifyPackageRequest
                {
                    Id = packageMetadata.Id,
                    Version = packageMetadata.Version.ToStringSafe(),
                    LicenseUrl = packageMetadata.LicenseUrl.ToStringSafe(),
                    Listed = true,
                    Edit = new EditPackageVersionRequest
                    {
                        Authors = packageMetadata.Authors.Flatten(),
                        Copyright = packageMetadata.Copyright,
                        Description = packageMetadata.Description,
                        IconUrl = packageMetadata.IconUrl.ToStringSafe(),
                        ProjectUrl = packageMetadata.ProjectUrl.ToStringSafe(),
                        ReleaseNotes = packageMetadata.ReleaseNotes,
                        RequiresLicenseAcceptance = packageMetadata.RequireLicenseAcceptance,
                        Summary = packageMetadata.Summary,
                        Tags = PackageHelper.ParseTags(packageMetadata.Tags),
                        VersionTitle = packageMetadata.Title,
                    }
                });
        }

        // The easiest way of keeping unit tests working.
        [NonAction]
        internal virtual Task<ActionResult> VerifyPackage(bool? listed)
        {
            return VerifyPackage(new VerifyPackageRequest { Listed = listed.GetValueOrDefault(true), Edit = null });
        }

        // Determine whether an 'Edit' string submitted differs from one read from the package.
        private static bool IsDifferent(string posted, string package)
        {
            if (String.IsNullOrEmpty(posted) || String.IsNullOrEmpty(package))
            {
                return String.IsNullOrEmpty(posted) != String.IsNullOrEmpty(package);
            }

            // Compare non-empty strings
            // Ignore those pesky '\r' characters which screw up comparisons.
            return !String.Equals(posted.Replace("\r", ""), package.Replace("\r", ""), StringComparison.Ordinal);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> VerifyPackage(VerifyPackageRequest formData)
        {
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            Package package;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    TempData["Message"] = "Your attempt to verify the package submission failed, because we could not find the uploaded package file. Please try again.";
                    return new RedirectResult(Url.UploadPackage());
                }

                INupkg nugetPackage = CreatePackage(uploadFile);

                // Rule out problem scenario with multiple tabs - verification request (possibly with edits) was submitted by user 
                // viewing a different package to what was actually most recently uploaded
                if (!(String.IsNullOrEmpty(formData.Id) || String.IsNullOrEmpty(formData.Version)))
                {
                    if (!(String.Equals(nugetPackage.Metadata.Id, formData.Id, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(nugetPackage.Metadata.Version.ToString(), formData.Version, StringComparison.OrdinalIgnoreCase)))
                    {
                        TempData["Message"] = "Your attempt to verify the package submission failed, because the package file appears to have changed. Please try again.";
                        return new RedirectResult(Url.VerifyPackage());
                    }
                }

                bool pendEdit = false;
                if (formData.Edit != null)
                {
                    pendEdit = pendEdit || formData.Edit.RequiresLicenseAcceptance != nugetPackage.Metadata.RequireLicenseAcceptance;

                    pendEdit = pendEdit || IsDifferent(formData.Edit.IconUrl, nugetPackage.Metadata.IconUrl.ToStringSafe());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ProjectUrl, nugetPackage.Metadata.ProjectUrl.ToStringSafe());

                    pendEdit = pendEdit || IsDifferent(formData.Edit.Authors, nugetPackage.Metadata.Authors.Flatten());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Copyright, nugetPackage.Metadata.Copyright);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Description, nugetPackage.Metadata.Description);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ReleaseNotes, nugetPackage.Metadata.ReleaseNotes);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Summary, nugetPackage.Metadata.Summary);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Tags, nugetPackage.Metadata.Tags);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.VersionTitle, nugetPackage.Metadata.Title);
                }

                // update relevant database tables
                package = _packageService.CreatePackage(nugetPackage, currentUser, commitChanges: false);
                Debug.Assert(package.PackageRegistration != null);

                _packageService.PublishPackage(package, commitChanges: false);

                if (pendEdit)
                {
                    // Add the edit request to a queue where it will be processed in the background.
                    _editPackageService.StartEditPackageRequest(package, formData.Edit, currentUser);
                }

                if (!formData.Listed)
                {
                    _packageService.MarkPackageUnlisted(package, commitChanges: false);
                }

                _autoCuratedPackageCmd.Execute(package, nugetPackage, commitChanges: false);

                // save package to blob storage
                uploadFile.Position = 0;
                await _packageFileService.SavePackageFileAsync(package, uploadFile);

                // commit all changes to database as an atomic transaction
                _entitiesContext.SaveChanges();

                // tell Lucene to update index for the new package
                _indexingService.UpdateIndex();

                // If we're pushing a new stable version of NuGet.CommandLine, update the extracted executable.
                if (package.PackageRegistration.Id.Equals(Constants.NuGetCommandLinePackageId, StringComparison.OrdinalIgnoreCase) &&
                    package.IsLatestStable)
                {
                    await _nugetExeDownloaderService.UpdateExecutableAsync(nugetPackage);
                }
            }

            // delete the uploaded binary in the Uploads container
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

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
        protected internal virtual INupkg CreatePackage(Stream stream)
        {
            return new Nupkg(stream, leaveOpen: false);
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
