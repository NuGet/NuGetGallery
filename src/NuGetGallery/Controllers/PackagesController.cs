// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.OData;
using NuGetGallery.Packaging;
using PoliteCaptcha;

namespace NuGetGallery
{
    public partial class PackagesController
        : AppController
    {
        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        private readonly IAutomaticallyCuratePackageCommand _autoCuratedPackageCmd;
        private readonly IAppConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly ISearchService _searchService;
        private readonly IUploadFileService _uploadFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IIndexingService _indexingService;
        private readonly ICacheService _cacheService;
        private readonly EditPackageService _editPackageService;
        private readonly IPackageDeleteService _packageDeleteService;
        private readonly ISupportRequestService _supportRequestService;

        public PackagesController(
            IPackageService packageService,
            IUploadFileService uploadFileService,
            IMessageService messageService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratedPackageCmd,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IAppConfiguration config,
            IIndexingService indexingService,
            ICacheService cacheService,
            EditPackageService editPackageService,
            IPackageDeleteService packageDeleteService,
            ISupportRequestService supportRequestService)
        {
            _packageService = packageService;
            _uploadFileService = uploadFileService;
            _messageService = messageService;
            _searchService = searchService;
            _autoCuratedPackageCmd = autoCuratedPackageCmd;
            _packageFileService = packageFileService;
            _entitiesContext = entitiesContext;
            _config = config;
            _indexingService = indexingService;
            _cacheService = cacheService;
            _editPackageService = editPackageService;
            _packageDeleteService = packageDeleteService;
            _supportRequestService = supportRequestService;
        }

        [Authorize]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public virtual ActionResult UploadPackageProgress()
        {
            string username = User.Identity.Name;

            AsyncFileUploadProgress progress = _cacheService.GetProgress(username);
            if (progress == null)
            {
                return HttpNotFound();
            }
            return Json(progress, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("undo pending edits")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UndoPendingEdits(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
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
                    await _entitiesContext.SaveChangesAsync();
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
        [RequiresAccountConfirmation("upload a package")]
        public async virtual Task<ActionResult> UploadPackage()
        {
            var currentUser = GetCurrentUser();

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return RedirectToRoute(RouteName.VerifyPackage);
                }
            }

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<ActionResult> UploadPackage(HttpPostedFileBase uploadFile)
        {
            var currentUser = GetCurrentUser();

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
                using (var archive = new ZipArchive(uploadStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var reference = DateTime.UtcNow.AddDays(1); // allow "some" clock skew

                    var entryInTheFuture = archive.Entries.FirstOrDefault(
                        e => e.LastWriteTime.UtcDateTime > reference);

                    if (entryInTheFuture != null)
                    {
                        ModelState.AddModelError(String.Empty, string.Format(
                           CultureInfo.CurrentCulture,
                           Strings.PackageEntryFromTheFuture,
                           entryInTheFuture.Name));

                        return View();
                    }
                }

                PackageArchiveReader packageArchiveReader;
                try
                {
                    packageArchiveReader = CreatePackage(uploadStream);

                    _packageService.EnsureValid(packageArchiveReader);
                }
                catch (InvalidPackageException ipex)
                {
                    ipex.Log();
                    ModelState.AddModelError(String.Empty, ipex.Message);
                    return View();
                }
                catch (InvalidDataException idex)
                {
                    idex.Log();
                    ModelState.AddModelError(String.Empty, idex.Message);
                    return View();
                }
                catch (EntityException enex)
                {
                    enex.Log();
                    ModelState.AddModelError(String.Empty, enex.Message);
                    return View();
                }
                catch (Exception ex)
                {
                    ex.Log();
                    ModelState.AddModelError(String.Empty, Strings.FailedToReadUploadFile);
                    return View();
                }
                finally
                {
                    _cacheService.RemoveProgress(currentUser.Username);
                }

                NuspecReader nuspec;
                var errors = ManifestValidator.Validate(packageArchiveReader.GetNuspec(), out nuspec).ToArray();
                if (errors.Length > 0)
                {
                    foreach (var error in errors)
                    {
                        ModelState.AddModelError(String.Empty, error.ErrorMessage);
                    }
                    return View();
                }

                // Check min client version
                if (nuspec.GetMinClientVersion() > Constants.MaxSupportedMinClientVersion)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UploadPackage_MinClientVersionOutOfRange,
                            nuspec.GetMinClientVersion()));
                    return View();
                }

                var packageRegistration = _packageService.FindPackageRegistrationById(nuspec.GetId());
                if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
                {
                    ModelState.AddModelError(
                        string.Empty, string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                    return View();
                }

                var package = _packageService.FindPackageByIdAndVersion(nuspec.GetId(), nuspec.GetVersion().ToStringSafe());
                if (package != null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        string.Format(
                            CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                    return View();
                }

                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, uploadStream);
            }

            return RedirectToRoute(RouteName.VerifyPackage);
        }

        public virtual async Task<ActionResult> DisplayPackage(string id, string version)
        {
            string normalized = NuGetVersionNormalizer.Normalize(version);
            if (!string.Equals(version, normalized))
            {
                // Permanent redirect to the normalized one (to avoid multiple URLs for the same content)
                return RedirectToActionPermanent("DisplayPackage", new { id = id, version = normalized });
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var packageHistory = package.PackageRegistration.Packages.ToList()
                .OrderByDescending(p => new NuGetVersion(p.Version));

            var model = new DisplayPackageViewModel(package, packageHistory);

            if (package.IsOwner(User))
            {
                // Tell logged-in package owners not to cache the package page,
                // so they won't be confused about the state of pending edits.
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

            var externalSearchService = _searchService as ExternalSearchService;
            if (_searchService.ContainsAllVersions && externalSearchService != null)
            {
                var isIndexedCacheKey = $"IsIndexed_{package.PackageRegistration.Id}_{package.Version}";
                var isIndexed = HttpContext.Cache.Get(isIndexedCacheKey) as bool?;
                if (!isIndexed.HasValue)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(
                            "id:\"" + package.PackageRegistration.Id + "\" AND version:\"" + package.Version + "\"",
                            1, null, SearchFilter.ODataSearchContext);

                    searchFilter.IncludePrerelease = true;
                    searchFilter.IncludeAllVersions = true;

                    var results = await externalSearchService.RawSearch(searchFilter);

                    isIndexed = results.Hits > 0;

                    var expiration = Cache.NoAbsoluteExpiration;
                    if (!isIndexed.Value)
                    {
                        expiration = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
                    }

                    HttpContext.Cache.Add(isIndexedCacheKey,
                        isIndexed,
                        null,
                        expiration,
                        Cache.NoSlidingExpiration,
                        CacheItemPriority.Default, null);
                }

                model.IsIndexed = isIndexed;
            }

            ViewBag.FacebookAppID = _config.FacebookAppId;
            return View(model);
        }

        public virtual async Task<ActionResult> ListPackages(string q, int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            q = (q ?? string.Empty).Trim();

            // We are not going to SQL here anyway, but our request logs do show some attempts to SQL injection.
            // The below code just fails out those requests early.
            if (q.ToLowerInvariant().Contains("char(")
                || q.ToLowerInvariant().Contains("union select")
                || q.ToLowerInvariant().Contains("/*")
                || q.ToLowerInvariant().Contains("--"))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            SearchResults results;

            // fetch most common query from cache to relieve load on the search service
            if (string.IsNullOrEmpty(q) && page == 1)
            {
                var cachedResults = HttpContext.Cache.Get("DefaultSearchResults");
                if (cachedResults == null)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(q, page, null, SearchFilter.UISearchContext);
                    results = await _searchService.Search(searchFilter);

                    // note: this is a per instance cache
                    HttpContext.Cache.Add(
                        "DefaultSearchResults",
                        results,
                        null,
                        DateTime.UtcNow.AddMinutes(10),
                        Cache.NoSlidingExpiration,
                        CacheItemPriority.Default, null);
                }
                else
                {
                    // default for /packages view
                    results = (SearchResults)cachedResults;
                }
            }
            else
            {
                var searchFilter = SearchAdaptor.GetSearchFilter(q, page, null, SearchFilter.UISearchContext);
                results = await _searchService.Search(searchFilter);
            }

            int totalHits = results.Hits;
            if (page == 1 && !results.Data.Any())
            {
                // In the event the index wasn't updated, we may get an incorrect count.
                totalHits = 0;
            }

            var viewModel = new PackageListViewModel(
                results.Data,
                results.IndexTimestampUtc,
                q,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        // NOTE: Intentionally NOT requiring authentication
        private static readonly ReportPackageReason[] ReportOtherPackageReasons = new[] {
            ReportPackageReason.IsFraudulent,
            ReportPackageReason.ViolatesALicenseIOwn,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.HasABugOrFailedToInstall,
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
                CopySender = true,
            };

            if (Request.IsAuthenticated)
            {
                var user = GetCurrentUser();

                // If user logged on in as owner a different tab, then clicked the link, we can redirect them to ReportMyPackage
                if (package.IsOwner(user))
                {
                    return RedirectToAction("ReportMyPackage", new { id, version });
                }

                if (user.Confirmed)
                {
                    model.ConfirmedUser = true;
                }
            }

            ViewData[Constants.ReturnUrlViewDataKey] = Url.Action("ReportMyPackage", new { id, version });
            return View(model);
        }

        private static readonly ReportPackageReason[] ReportMyPackageReasons = {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.PublishedWithWrongVersion,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        [Authorize]
        [RequiresAccountConfirmation("contact support about your package")]
        public virtual ActionResult ReportMyPackage(string id, string version)
        {
            var user = GetCurrentUser();

            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            // If user hit this url by constructing it manually but is not the owner, redirect them to ReportAbuse
            if (!(User.IsInRole(Constants.AdminRoleName) || package.IsOwner(user)))
            {
                return RedirectToAction("ReportAbuse", new { id, version });
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportMyPackageReasons,
                ConfirmedUser = user.Confirmed,
                PackageId = id,
                PackageVersion = package.Version,
                CopySender = true,
                Signature = user.Username
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual async Task<ActionResult> ReportAbuse(string id, string version, ReportAbuseViewModel reportForm)
        {
            // Html Encode the message
            reportForm.Message = System.Web.HttpUtility.HtmlEncode(reportForm.Message);

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
                user = GetCurrentUser();
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
                Url = Url,
                CopySender = reportForm.CopySender,
                Signature = reportForm.Signature
            };

            var subject = $"Support Request for '{package.PackageRegistration.Id}' version {package.Version}";
            var requestorEmailAddress = user != null ? user.EmailAddress : reportForm.Email;
            var reason = EnumHelper.GetDescription(reportForm.Reason.Value);

            await _supportRequestService.AddNewSupportRequestAsync(subject, reportForm.Message, requestorEmailAddress, reason, user, package);

            _messageService.ReportAbuse(request);

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
        }

        [HttpPost]
        [Authorize]
        [RequiresAccountConfirmation("contact support about your package")]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual async Task<ActionResult> ReportMyPackage(string id, string version, ReportAbuseViewModel reportForm)
        {
            // Html Encode the message
            reportForm.Message = System.Web.HttpUtility.HtmlEncode(reportForm.Message);

            if (!ModelState.IsValid)
            {
                return ReportMyPackage(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            MailAddress from = user.ToMailAddress();

            var request = new ReportPackageRequest
            {
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                Url = Url,
                CopySender = reportForm.CopySender
            };

            var subject = $"Owner Support Request for '{package.PackageRegistration.Id}' version {package.Version}";
            var requestorEmailAddress = user != null ? user.EmailAddress : reportForm.Email;
            var reason = EnumHelper.GetDescription(reportForm.Reason.Value);

            await _supportRequestService.AddNewSupportRequestAsync(subject, reportForm.Message, requestorEmailAddress, reason, user, package);

            _messageService.ReportMyPackage(request);

            TempData["Message"] = "Your support request has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
        }

        [Authorize]
        [RequiresAccountConfirmation("contact package owners")]
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
                Owners = package.Owners.Where(u => u.EmailAllowed),
                CopySender = true,
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual ActionResult ContactOwners(string id, ContactOwnersViewModel contactForm)
        {
            // Html Encode the message
            contactForm.Message = System.Web.HttpUtility.HtmlEncode(contactForm.Message);

            if (!ModelState.IsValid)
            {
                return ContactOwners(id);
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            var fromAddress = new MailAddress(user.EmailAddress, user.Username);
            _messageService.SendContactOwnersMessage(
                fromAddress,
                package,
                contactForm.Message,
                Url.Action(
                    actionName: "Account",
                    controllerName: "Users",
                    routeValues: null,
                    protocol: Request.Url.Scheme),
                contactForm.CopySender);

            string message = String.Format(CultureInfo.CurrentCulture, "Your message has been sent to the owners of {0}.", id);
            TempData["Message"] = message;
            return RedirectToAction(
                actionName: "DisplayPackage",
                controllerName: "Packages",
                routeValues: new
                {
                    id,
                    version = (string)null
                });
        }

        // This is the page that explains why there's no download link.
        public virtual ActionResult Download()
        {
            return View();
        }

        [Authorize]
        public virtual ActionResult ManagePackageOwners(string id)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, string.Empty);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new ManagePackageOwnersViewModel(package, User);

            return View(model);
        }

        [Authorize]
        [RequiresAccountConfirmation("delete a package")]
        public virtual ActionResult Delete(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DeletePackageViewModel(package, ReportMyPackageReasons);
            return View(model);
        }

        [Authorize(Roles = "Admins")]
        [RequiresAccountConfirmation("reflow a package")]
        public virtual async Task<ActionResult> Reflow(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }
            
            var reflowPackageService = new ReflowPackageService(
                _entitiesContext, 
                (PackageService) _packageService,
                _packageFileService);

            try
            {
                await reflowPackageService.ReflowAsync(id, version);

                TempData["Message"] =
                    "The package is being reflowed. It may take a while for this change to propagate through our system.";
            }
            catch (Exception ex)
            {
                TempData["Message"] =
                    $"An error occurred while reflowing the package. {ex.Message}";

                QuietLog.LogHandledException(ex);
            }

            return SafeRedirect(Url.Package(id, version));
        }

        [Authorize(Roles = "Admins")]
        [HttpPost]
        [RequiresAccountConfirmation("delete a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Delete(DeletePackagesRequest deletePackagesRequest)
        {
            var packagesToDelete = new List<Package>();

            if (ModelState.IsValid)
            {
                // Get the packages to delete
                foreach (var package in deletePackagesRequest.Packages)
                {
                    var split = package.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length == 2)
                    {
                        var packageToDelete = _packageService.FindPackageByIdAndVersion(split[0], split[1], allowPrerelease: true);
                        if (packageToDelete != null)
                        {
                            packagesToDelete.Add(packageToDelete);
                        }
                    }
                }

                // Perform delete
                if (deletePackagesRequest.SoftDelete)
                {
                    await _packageDeleteService.SoftDeletePackagesAsync(
                        packagesToDelete, GetCurrentUser(), EnumHelper.GetDescription(deletePackagesRequest.Reason.Value),
                        deletePackagesRequest.Signature);
                }
                else
                {
                    await _packageDeleteService.HardDeletePackagesAsync(
                        packagesToDelete, GetCurrentUser(), EnumHelper.GetDescription(deletePackagesRequest.Reason.Value),
                        deletePackagesRequest.Signature,
                        deletePackagesRequest.DeleteEmptyPackageRegistration);
                }

                // Redirect out
                TempData["Message"] =
                    "We're performing the package delete right now. It may take a while for this change to propagate through our system.";

                return Redirect("/");
            }

            if (!deletePackagesRequest.Packages.Any())
            {
                return HttpNotFound();
            }

            var firstPackage = packagesToDelete.First();
            return Delete(firstPackage.PackageRegistration.Id, firstPackage.Version);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("unlist a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UpdateListed(string id, string version, bool? listed)
        {
            // Edit does exactly the same thing that Delete used to do... REUSE ALL THE CODE!
            return await Edit(id, version, listed, Url.Package);
        }

        [Authorize]
        [RequiresAccountConfirmation("edit a package")]
        public virtual ActionResult Edit(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
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
                    .OrderByDescending(p => new NuGetVersion(p.Version), Comparer<NuGetVersion>.Create((a, b) => a.CompareTo(b)))
                    .ToList(),
            };

            var pendingMetadata = _editPackageService.GetPendingMetadata(package);
            model.Edit = new EditPackageVersionRequest(package, pendingMetadata);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("edit a package")]
        public virtual async Task<ActionResult> Edit(string id, string version, EditPackageRequest formData, string returnUrl)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            var user = GetCurrentUser();
            if (!ModelState.IsValid)
            {
                return EditFailed(id, formData, package);
            }

            // Add the edit request to a queue where it will be processed in the background.
            if (formData.Edit != null)
            {
                try
                {
                    _editPackageService.StartEditPackageRequest(package, formData.Edit, user);
                    await _entitiesContext.SaveChangesAsync();
                }
                catch (EntityException ex)
                {
                    ModelState.AddModelError("Edit.VersionTitle", ex.Message);

                    return EditFailed(id, formData, package);
                }
            }

            return SafeRedirect(returnUrl ?? Url.Package(id, version));
        }

        private ActionResult EditFailed(string id, EditPackageRequest formData, Package package)
        {
            formData.PackageId = package.PackageRegistration.Id;
            formData.PackageTitle = package.Title;
            formData.Version = package.Version;

            var packageRegistration = _packageService.FindPackageRegistrationById(id);
            formData.PackageVersions = packageRegistration.Packages
                .OrderByDescending(p => new NuGetVersion(p.Version), Comparer<NuGetVersion>.Create((a, b) => a.CompareTo(b)))
                .ToList();

            return View(formData);
        }

        [Authorize]
        [RequiresAccountConfirmation("accept ownership of a package")]
        public virtual async Task<ActionResult> ConfirmOwner(string id, string username, string token)
        {
            if (String.IsNullOrEmpty(token))
            {
                return HttpNotFound();
            }

            if (!String.Equals(username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new PackageOwnerConfirmationModel()
                {
                    Username = username,
                    Result = ConfirmOwnershipResult.NotYourRequest
                });
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            ConfirmOwnershipResult result = await _packageService.ConfirmPackageOwnerAsync(package, user, token);

            var model = new PackageOwnerConfirmationModel
            {
                Result = result,
                PackageId = package.Id
            };

            return View(model);
        }

        internal virtual async Task<ActionResult> Edit(string id, string version, bool? listed, Func<Package, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            string action;
            if (!(listed ?? false))
            {
                action = "unlisted";
                await _packageService.MarkPackageUnlistedAsync(package);
            }
            else
            {
                action = "listed";
                await _packageService.MarkPackageListedAsync(package);
            }
            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture,
                "The package has been {0}. It may take several hours for this change to propagate through our system.",
                action);

            // Update the index
            _indexingService.UpdatePackage(package);
            return Redirect(urlFactory(package));
        }

        [Authorize]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<ActionResult> VerifyPackage()
        {
            var currentUser = GetCurrentUser();

            PackageMetadata packageMetadata;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return RedirectToRoute(RouteName.UploadPackage);
                }

                var package = await SafeCreatePackage(currentUser, uploadFile);
                if (package == null)
                {
                    return Redirect(Url.UploadPackage());
                }

                try
                {
                    packageMetadata = PackageMetadata.FromNuspecReader(
                        package.GetNuspecReader());
                }
                catch (Exception ex)
                {
                    TempData["Message"] = ex.GetUserSafeMessage();
                    return Redirect(Url.UploadPackage());
                }
            }

            var model = new VerifyPackageRequest
            {
                Id = packageMetadata.Id,
                Version = packageMetadata.Version.ToNormalizedStringSafe(),
                LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull(),
                Listed = true,
                Language = packageMetadata.Language,
                MinClientVersion = packageMetadata.MinClientVersion,
                FrameworkReferenceGroups = packageMetadata.GetFrameworkReferenceGroups(),
                Dependencies = new DependencySetsViewModel(
                    packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable()),
                DevelopmentDependency = packageMetadata.GetValueFromMetadata("developmentDependency"),
                Edit = new EditPackageVersionRequest
                {
                    Authors = packageMetadata.Authors.Flatten(),
                    Copyright = packageMetadata.Copyright,
                    Description = packageMetadata.Description,
                    IconUrl = packageMetadata.IconUrl.ToEncodedUrlStringOrNull(),
                    LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull(),
                    ProjectUrl = packageMetadata.ProjectUrl.ToEncodedUrlStringOrNull(),
                    ReleaseNotes = packageMetadata.ReleaseNotes,
                    RequiresLicenseAcceptance = packageMetadata.RequireLicenseAcceptance,
                    Summary = packageMetadata.Summary,
                    Tags = PackageHelper.ParseTags(packageMetadata.Tags),
                    VersionTitle = packageMetadata.Title,
                }
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("upload a package")]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        public virtual async Task<ActionResult> VerifyPackage(VerifyPackageRequest formData)
        {
            var currentUser = GetCurrentUser();

            Package package;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    TempData["Message"] = "Your attempt to verify the package submission failed, because we could not find the uploaded package file. Please try again.";
                    return new RedirectResult(Url.UploadPackage());
                }

                var nugetPackage = await SafeCreatePackage(currentUser, uploadFile);
                if (nugetPackage == null)
                {
                    // Send the user back
                    return new RedirectResult(Url.UploadPackage());
                }
                Debug.Assert(nugetPackage != null);

                var packageMetadata = PackageMetadata.FromNuspecReader(
                    nugetPackage.GetNuspecReader());

                // Rule out problem scenario with multiple tabs - verification request (possibly with edits) was submitted by user
                // viewing a different package to what was actually most recently uploaded
                if (!(String.IsNullOrEmpty(formData.Id) || String.IsNullOrEmpty(formData.Version)))
                {
                    if (!(String.Equals(packageMetadata.Id, formData.Id, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(packageMetadata.Version.ToNormalizedString(), formData.Version, StringComparison.OrdinalIgnoreCase)))
                    {
                        TempData["Message"] = "Your attempt to verify the package submission failed, because the package file appears to have changed. Please try again.";
                        return new RedirectResult(Url.VerifyPackage());
                    }
                }

                bool pendEdit = false;
                if (formData.Edit != null)
                {
                    pendEdit = pendEdit || formData.Edit.RequiresLicenseAcceptance != packageMetadata.RequireLicenseAcceptance;

                    pendEdit = pendEdit || IsDifferent(formData.Edit.IconUrl, packageMetadata.IconUrl.ToEncodedUrlStringOrNull());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ProjectUrl, packageMetadata.ProjectUrl.ToEncodedUrlStringOrNull());

                    pendEdit = pendEdit || IsDifferent(formData.Edit.Authors, packageMetadata.Authors.Flatten());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Copyright, packageMetadata.Copyright);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Description, packageMetadata.Description);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ReleaseNotes, packageMetadata.ReleaseNotes);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Summary, packageMetadata.Summary);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Tags, packageMetadata.Tags);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.VersionTitle, packageMetadata.Title);
                }

                var packageStreamMetadata = new PackageStreamMetadata
                {
                    HashAlgorithm = Constants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(uploadFile.AsSeekableStream()),
                    Size = uploadFile.Length,
                };

                // update relevant database tables
                try
                {
                    package = await _packageService.CreatePackageAsync(nugetPackage, packageStreamMetadata, currentUser, commitChanges: false);
                    Debug.Assert(package.PackageRegistration != null);
                }
                catch (EntityException ex)
                {
                    TempData["Message"] = ex.Message;
                    return Redirect(Url.UploadPackage());
                }

                await _packageService.PublishPackageAsync(package, commitChanges: false);

                if (pendEdit)
                {
                    // Add the edit request to a queue where it will be processed in the background.
                    _editPackageService.StartEditPackageRequest(package, formData.Edit, currentUser);
                }

                if (!formData.Listed)
                {
                    await _packageService.MarkPackageUnlistedAsync(package, commitChanges: false);
                }

                await _autoCuratedPackageCmd.ExecuteAsync(package, nugetPackage, commitChanges: false);

                // save package to blob storage
                uploadFile.Position = 0;
                await _packageFileService.SavePackageFileAsync(package, uploadFile.AsSeekableStream());

                // commit all changes to database as an atomic transaction
                await _entitiesContext.SaveChangesAsync();

                // tell Lucene to update index for the new package
                _indexingService.UpdateIndex();

                _messageService.SendPackageAddedNotice(package,
                    Url.Action("DisplayPackage", "Packages", routeValues: new { id = package.PackageRegistration.Id, version = package.Version }, protocol: Request.Url.Scheme),
                    Url.Action("ReportMyPackage", "Packages", routeValues: new { id = package.PackageRegistration.Id, version = package.Version }, protocol: Request.Url.Scheme),
                    Url.Action("Account", "Users", routeValues: null, protocol: Request.Url.Scheme));
            }

            // delete the uploaded binary in the Uploads container
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture, Strings.SuccessfullyUploadedPackage, package.PackageRegistration.Id, package.Version);

            return RedirectToRoute(RouteName.DisplayPackage, new { package.PackageRegistration.Id, package.Version });
        }

        private async Task<PackageArchiveReader> SafeCreatePackage(NuGetGallery.User currentUser, Stream uploadFile)
        {
            Exception caught = null;
            PackageArchiveReader packageArchiveReader = null;
            try
            {
                packageArchiveReader = CreatePackage(uploadFile);
            }
            catch (InvalidPackageException ipex)
            {
                caught = ipex.AsUserSafeException();
            }
            catch (InvalidDataException idex)
            {
                caught = idex.AsUserSafeException();
            }
            catch (EntityException enex)
            {
                caught = enex.AsUserSafeException();
            }
            catch (Exception ex)
            {
                // Can't wait for Roslyn to let us await in Catch blocks :(
                caught = ex;
            }

            if (caught != null)
            {
                caught.Log();

                // Report the error
                TempData["Message"] = caught.GetUserSafeMessage();

                // Clear the upload
                await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);
            }

            return packageArchiveReader;
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> CancelUpload()
        {
            var currentUser = GetCurrentUser();
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            return RedirectToAction("UploadPackage");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> SetLicenseReportVisibility(string id, string version, bool visible)
        {
            return await SetLicenseReportVisibility(id, version, visible, Url.Package);
        }

        internal virtual async Task<ActionResult> SetLicenseReportVisibility(string id, string version, bool visible, Func<Package, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            await _packageService.SetLicenseReportVisibilityAsync(package, visible);

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture,
                "The license report for this package has been {0}. It may take several hours for this change to propagate through our system.",
                visible ? "enabled" : "disabled");

            // Update the index
            _indexingService.UpdatePackage(package);

            return Redirect(urlFactory(package));
        }

        // this methods exist to make unit testing easier
        protected internal virtual PackageArchiveReader CreatePackage(Stream stream)
        {
            try
            {
                return new PackageArchiveReader(stream, leaveStreamOpen: true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
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
    }
}
