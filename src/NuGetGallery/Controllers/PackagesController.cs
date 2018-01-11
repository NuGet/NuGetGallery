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
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.OData;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using PoliteCaptcha;

namespace NuGetGallery
{
    public partial class PackagesController
        : AppController
    {
        private static readonly IReadOnlyList<ReportPackageReason> ReportAbuseReasons = new[]
        {
            ReportPackageReason.ViolatesALicenseIOwn,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.HasABugOrFailedToInstall,
            ReportPackageReason.Other
        };

        private static readonly IReadOnlyList<ReportPackageReason> ReportMyPackageReasons = new[]
        {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        private static readonly IReadOnlyList<ReportPackageReason> DeleteReasons = new[]
        {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
        };

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
        private readonly IUserService _userService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IIndexingService _indexingService;
        private readonly ICacheService _cacheService;
        private readonly IPackageDeleteService _packageDeleteService;
        private readonly ISupportRequestService _supportRequestService;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;
        private readonly ISecurityPolicyService _securityPolicyService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IPackageUploadService _packageUploadService;
        private readonly IReadMeService _readMeService;
        private readonly IValidationService _validationService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;

        public PackagesController(
            IPackageService packageService,
            IUploadFileService uploadFileService,
            IUserService userService,
            IMessageService messageService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratedPackageCmd,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IAppConfiguration config,
            IIndexingService indexingService,
            ICacheService cacheService,
            IPackageDeleteService packageDeleteService,
            ISupportRequestService supportRequestService,
            IAuditingService auditingService,
            ITelemetryService telemetryService,
            ISecurityPolicyService securityPolicyService,
            IReservedNamespaceService reservedNamespaceService,
            IPackageUploadService packageUploadService,
            IReadMeService readMeService,
            IValidationService validationService,
            IPackageOwnershipManagementService packageOwnershipManagementService)
        {
            _packageService = packageService;
            _uploadFileService = uploadFileService;
            _userService = userService;
            _messageService = messageService;
            _searchService = searchService;
            _autoCuratedPackageCmd = autoCuratedPackageCmd;
            _packageFileService = packageFileService;
            _entitiesContext = entitiesContext;
            _config = config;
            _indexingService = indexingService;
            _cacheService = cacheService;
            _packageDeleteService = packageDeleteService;
            _supportRequestService = supportRequestService;
            _auditingService = auditingService;
            _telemetryService = telemetryService;
            _securityPolicyService = securityPolicyService;
            _reservedNamespaceService = reservedNamespaceService;
            _packageUploadService = packageUploadService;
            _readMeService = readMeService;
            _validationService = validationService;
            _packageOwnershipManagementService = packageOwnershipManagementService;
        }

        [HttpGet]
        [Authorize]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public virtual JsonResult UploadPackageProgress()
        {
            string username = User.Identity.Name;

            AsyncFileUploadProgress progress = _cacheService.GetProgress(username);
            if (progress == null)
            {
                return Json(404, null, JsonRequestBehavior.AllowGet);
            }

            return Json(progress, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [RequiresAccountConfirmation("upload a package")]
        public async virtual Task<ActionResult> UploadPackage()
        {
            var currentUser = GetCurrentUser();
            var model = new SubmitPackageRequest();
            PackageMetadata packageMetadata;

            using (var uploadedFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadedFile != null)
                {

                    var package = await SafeCreatePackage(currentUser, uploadedFile);
                    if (package == null)
                    {
                        return View(model);
                    }

                    try
                    {
                        packageMetadata = PackageMetadata.FromNuspecReader(
                            package.GetNuspecReader());
                    }
                    catch (Exception ex)
                    {
                        _telemetryService.TraceException(ex);

                        TempData["Message"] = ex.GetUserSafeMessage();
                        return View(model);
                    }

                    model.IsUploadInProgress = true;

                    var existingPackageRegistration = _packageService.FindPackageRegistrationById(packageMetadata.Id);
                    bool isAllowed;
                    IEnumerable<User> accountsAllowedOnBehalfOf = Enumerable.Empty<User>();
                    if (existingPackageRegistration == null)
                    {
                        isAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissionsOnBehalfOfAnyAccount(currentUser, new ActionOnNewPackageContext(packageMetadata.Id, _reservedNamespaceService), out accountsAllowedOnBehalfOf) == PermissionsCheckResult.Allowed;
                    }
                    else
                    {
                        isAllowed = ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissionsOnBehalfOfAnyAccount(currentUser, existingPackageRegistration, out accountsAllowedOnBehalfOf) == PermissionsCheckResult.Allowed;
                    }

                    if (!isAllowed)
                    {
                        // If the current user cannot upload the package on behalf of any of the existing owners, show the current user as the only possible owner in the upload form.
                        // The package upload will be rejected by submitting the form.
                        // Related: https://github.com/NuGet/NuGetGallery/issues/5043
                        accountsAllowedOnBehalfOf = new[] { currentUser };
                    }

                    var verifyRequest = new VerifyPackageRequest(packageMetadata, accountsAllowedOnBehalfOf);

                    model.InProgressUpload = verifyRequest;
                }
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<JsonResult> UploadPackage(HttpPostedFileBase uploadFile)
        {
            var currentUser = GetCurrentUser();

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return Json(409, new[] { Strings.UploadPackage_UploadInProgress });
                }
            }

            if (uploadFile == null)
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileIsRequired);
                return Json(400, new[] { Strings.UploadFileIsRequired });
            }

            if (!Path.GetExtension(uploadFile.FileName).Equals(CoreConstants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileMustBeNuGetPackage);
                return Json(400, new[] { Strings.UploadFileMustBeNuGetPackage });
            }

            // If the current user cannot upload the package on behalf of any of the existing owners, show the current user as the only possible owner in the upload form.
            // If the current user doesn't have the rights to upload the package, the package upload will be rejected by submitting the form.
            // Related: https://github.com/NuGet/NuGetGallery/issues/5043
            IEnumerable<User> accountsAllowedOnBehalfOf = new[] { currentUser };

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

                        return Json(400, new[] {
                            string.Format(CultureInfo.CurrentCulture, Strings.PackageEntryFromTheFuture, entryInTheFuture.Name) });
                    }
                }

                PackageArchiveReader packageArchiveReader;
                try
                {
                    packageArchiveReader = CreatePackage(uploadStream);

                    _packageService.EnsureValid(packageArchiveReader);
                }
                catch (Exception ex)
                {
                    ex.Log();

                    var message = Strings.FailedToReadUploadFile;
                    if (ex is InvalidPackageException || ex is InvalidDataException || ex is EntityException)
                    {
                        message = ex.Message;
                    }

                    ModelState.AddModelError(String.Empty, message);

                    return Json(400, new[] { message });
                }
                finally
                {
                    _cacheService.RemoveProgress(currentUser.Username);
                }

                NuspecReader nuspec;
                var errors = ManifestValidator.Validate(packageArchiveReader.GetNuspec(), out nuspec).ToArray();
                if (errors.Length > 0)
                {
                    var errorStrings = new List<string>();
                    foreach (var error in errors)
                    {
                        errorStrings.Add(error.ErrorMessage);
                        ModelState.AddModelError(String.Empty, error.ErrorMessage);
                    }

                    return Json(400, errorStrings);
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

                    return Json(400, new[] {
                        string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_MinClientVersionOutOfRange, nuspec.GetMinClientVersion()) });
                }

                var id = nuspec.GetId();
                var existingPackageRegistration = _packageService.FindPackageRegistrationById(id);
                // For a new package id verify if the user is allowed to use it.
                if (existingPackageRegistration == null &&
                    ActionsRequiringPermissions.UploadNewPackageId.CheckPermissionsOnBehalfOfAnyAccount(
                        currentUser, new ActionOnNewPackageContext(id, _reservedNamespaceService)) != PermissionsCheckResult.Allowed)
                {
                    ModelState.AddModelError(
                        string.Empty, string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict));

                    var version = nuspec.GetVersion().ToNormalizedString();
                    _telemetryService.TrackPackagePushNamespaceConflictEvent(id, version, currentUser, User.Identity);

                    return Json(409, new string[] { string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict) });
                }

                // For existing package id verify if it is owned by the current user
                if (existingPackageRegistration != null)
                {
                    if (ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissionsOnBehalfOfAnyAccount(currentUser, existingPackageRegistration) != PermissionsCheckResult.Allowed)
                    {
                        ModelState.AddModelError(
                          string.Empty, string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, existingPackageRegistration.Id));

                        return Json(409, new[] { string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, existingPackageRegistration.Id) });
                    }

                    if (existingPackageRegistration.IsLocked)
                    {
                        return Json(403, new[] { string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, existingPackageRegistration.Id) });
                    }
                }

                var nuspecVersion = nuspec.GetVersion();
                var existingPackage = _packageService.FindPackageByIdAndVersionStrict(nuspec.GetId(), nuspecVersion.ToStringSafe());
                if (existingPackage != null)
                {
                    // Determine if the package versions only differ by metadata, 
                    // and provide the most optimal the user-facing error message.
                    var existingPackageVersion = new NuGetVersion(existingPackage.Version);
                    String message = string.Empty;
                    if ((existingPackageVersion.HasMetadata || nuspecVersion.HasMetadata)
                        && !string.Equals(existingPackageVersion.Metadata, nuspecVersion.Metadata))
                    {
                        message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.PackageVersionDiffersOnlyByMetadataAndCannotBeModified,
                                existingPackage.PackageRegistration.Id,
                                existingPackage.Version);
                    }
                    else
                    {
                        message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.PackageExistsAndCannotBeModified,
                                existingPackage.PackageRegistration.Id,
                                existingPackage.Version);
                    }

                    ModelState.AddModelError(
                        string.Empty,
                        message);

                    return Json(409, new[] { message });
                }

                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, uploadStream);
            }

            PackageMetadata packageMetadata;
            using (Stream uploadedFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadedFile == null)
                {
                    ModelState.AddModelError(String.Empty, Strings.UploadFileIsRequired);
                    return Json(400, new[] { Strings.UploadFileIsRequired });
                }

                var package = await SafeCreatePackage(currentUser, uploadedFile);
                if (package == null)
                {
                    return Json(400, new[] { Strings.UploadFileIsRequired });
                }

                try
                {
                    packageMetadata = PackageMetadata.FromNuspecReader(
                        package.GetNuspecReader());
                }
                catch (Exception ex)
                {
                    _telemetryService.TraceException(ex);

                    return Json(400, new[] { ex.GetUserSafeMessage() });
                }
            }

            var model = new VerifyPackageRequest(packageMetadata, accountsAllowedOnBehalfOf);

            return Json(model);
        }

        public virtual async Task<ActionResult> DisplayPackage(string id, string version)
        {
            string normalized = NuGetVersionFormatter.Normalize(version);
            if (!string.Equals(version, normalized))
            {
                // Permanent redirect to the normalized one (to avoid multiple URLs for the same content)
                return RedirectToActionPermanent("DisplayPackage", new { id = id, version = normalized });
            }

            Package package;
            if (version != null && version.Equals(Constants.AbsoluteLatestUrlString, StringComparison.InvariantCultureIgnoreCase))
            {
                package = _packageService.FindAbsoluteLatestPackageById(id, SemVerLevelKey.SemVer2);
            }
            else
            {
                package = _packageService.FindPackageByIdAndVersion(id, version, SemVerLevelKey.SemVer2);
            }

            // Validating packages should be hidden to everyone but the owners and admins.
            var currentUser = GetCurrentUser();
            if (package == null
                || ((package.PackageStatusKey == PackageStatus.Validating
                     || package.PackageStatusKey == PackageStatus.FailedValidation)
                    && ActionsRequiringPermissions.DisplayPrivatePackageMetadata.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) != PermissionsCheckResult.Allowed))
            {
                return HttpNotFound();
            }

            var packageHistory = package
                .PackageRegistration
                .Packages
                .ToList()
                .OrderByDescending(p => new NuGetVersion(p.Version));

            var model = new DisplayPackageViewModel(package, currentUser, packageHistory);

            model.ValidationIssues = _validationService.GetLatestValidationIssues(package);

            model.ReadMeHtml = await _readMeService.GetReadMeHtmlAsync(package);

            var externalSearchService = _searchService as ExternalSearchService;
            if (_searchService.ContainsAllVersions && externalSearchService != null)
            {
                var isIndexedCacheKey = $"IsIndexed_{package.PackageRegistration.Id}_{package.Version}";
                var isIndexed = HttpContext.Cache.Get(isIndexedCacheKey) as bool?;
                if (!isIndexed.HasValue)
                {
                    var normalizedRegistrationId = package.PackageRegistration.Id
                        .Normalize(NormalizationForm.FormC);

                    var searchFilter = SearchAdaptor.GetSearchFilter(
                            q: "id:\"" + normalizedRegistrationId + "\" AND version:\"" + package.Version + "\"",
                        page: 1,
                        includePrerelease: true,
                        sortOrder: null,
                        context: SearchFilter.ODataSearchContext,
                        semVerLevel: SemVerLevelKey.SemVerLevel2);

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

        public virtual async Task<ActionResult> ListPackages(PackageListSearchViewModel searchAndListModel)
        {
            var page = searchAndListModel.Page;
            var q = searchAndListModel.Q;
            var includePrerelease = searchAndListModel.Prerel ?? true;

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
            if (string.IsNullOrEmpty(q) && page == 1 && includePrerelease)
            {
                var cachedResults = HttpContext.Cache.Get("DefaultSearchResults");
                if (cachedResults == null)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(
                        q,
                        page,
                        includePrerelease: includePrerelease,
                        sortOrder: null,
                        context: SearchFilter.UISearchContext,
                        semVerLevel: SemVerLevelKey.SemVerLevel2);

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
                var searchFilter = SearchAdaptor.GetSearchFilter(
                    q,
                    page,
                    includePrerelease: includePrerelease,
                    sortOrder: null,
                    context: SearchFilter.UISearchContext,
                    semVerLevel: SemVerLevelKey.SemVerLevel2);

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
                GetCurrentUser(),
                results.IndexTimestampUtc,
                q,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url,
                includePrerelease);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        [HttpGet]
        public virtual ActionResult ReportAbuse(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportAbuseReasons,
                PackageId = id,
                PackageVersion = package.Version,
                CopySender = true,
            };

            if (Request.IsAuthenticated)
            {
                var user = GetCurrentUser();

                // If user logged on in as owner a different tab, then clicked the link, we can redirect them to ReportMyPackage
                if (ActionsRequiringPermissions.ReportPackageAsOwner.CheckPermissionsOnBehalfOfAnyAccount(user, package) == PermissionsCheckResult.Allowed)
                {
                    return RedirectToAction("ReportMyPackage", new { id, version });
                }

                if (user.Confirmed)
                {
                    model.ConfirmedUser = true;
                }
            }

            ViewData[Constants.ReturnUrlViewDataKey] = Url.ReportPackage(id, version);
            return View(model);
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("contact support about your package")]
        public virtual async Task<ActionResult> ReportMyPackage(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.ReportPackageAsOwner.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return RedirectToAction(nameof(ReportAbuse), new { id, version });
            }

            var allowDelete = await _packageDeleteService.CanPackageBeDeletedByUserAsync(package);

            var model = new ReportMyPackageViewModel
            {
                ReasonChoices = ReportMyPackageReasons,
                DeleteReasonChoices = DeleteReasons,
                ConfirmedUser = GetCurrentUser().Confirmed,
                PackageId = id,
                PackageVersion = package.Version,
                CopySender = true,
                AllowDelete = allowDelete,
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual async Task<ActionResult> ReportAbuse(string id, string version, ReportAbuseViewModel reportForm)
        {
            reportForm.Message = HttpUtility.HtmlEncode(reportForm.Message);

            if (reportForm.Reason == ReportPackageReason.ViolatesALicenseIOwn
                && string.IsNullOrWhiteSpace(reportForm.Signature))
            {
                ModelState.AddModelError(
                    nameof(ReportAbuseViewModel.Signature),
                    "The signature is required.");
            }

            if (!ModelState.IsValid)
            {
                return ReportAbuse(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
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
        public virtual async Task<ActionResult> ReportMyPackage(string id, string version, ReportMyPackageViewModel reportForm)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

            var failureResult = await ValidateReportMyPackageViewModel(reportForm, package);
            if (failureResult != null)
            {
                return failureResult;
            }

            // Override the copy sender and message fields if we are performing an auto-delete.
            if (reportForm.DeleteDecision == PackageDeleteDecision.DeletePackage)
            {
                reportForm.CopySender = false;
                reportForm.Message = Strings.UserPackageDeleteSupportRequestMessage;
            }

            var user = GetCurrentUser();
            var from = user.ToMailAddress();
            var subject = string.Format(
                Strings.OwnerSupportRequestSubjectFormat,
                package.PackageRegistration.Id,
                package.NormalizedVersion);
            var reason = EnumHelper.GetDescription(reportForm.Reason.Value);
            var supportRequest = await _supportRequestService.AddNewSupportRequestAsync(
                subject,
                reportForm.Message,
                from.Address,
                reason,
                user,
                package);

            var deleted = false;
            if (supportRequest != null
                && reportForm.DeleteDecision == PackageDeleteDecision.DeletePackage)
            {
                deleted = await DeletePackageOnBehalfOfUserAsync(package, user, reason, supportRequest);
            }

            if (!deleted)
            {
                NotifyReportMyPackageSupportRequest(reportForm, package, user, from);
            }

            return Redirect(Url.Package(package.PackageRegistration.Id, package.NormalizedVersion));
        }

        private async Task<ActionResult> ValidateReportMyPackageViewModel(ReportMyPackageViewModel reportForm, Package package)
        {
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.ReportPackageAsOwner.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return RedirectToAction(nameof(ReportAbuse), new { id = package.PackageRegistration.Id, version = package.NormalizedVersion });
            }

            reportForm.Message = HttpUtility.HtmlEncode(reportForm.Message);

            // Enforce the auto-delete rules.
            var allowDelete = false;
            if (reportForm.DeleteDecision != PackageDeleteDecision.ContactSupport)
            {
                allowDelete = await _packageDeleteService.CanPackageBeDeletedByUserAsync(package);
                if (!allowDelete)
                {
                    reportForm.DeleteDecision = null;
                }
            }

            // Require a delete decision if auto-delete is allowed and implied by the reason.
            if (allowDelete
                && reportForm.Reason.HasValue
                && DeleteReasons.Contains(reportForm.Reason.Value)
                && !reportForm.DeleteDecision.HasValue)
            {
                ModelState.AddModelError(
                    nameof(ReportMyPackageViewModel.DeleteDecision),
                    Strings.UserPackageDeleteDecisionIsRequired);
            }

            // Require the confirmation checkbox if we are performing an auto-delete.
            if (reportForm.DeleteDecision == PackageDeleteDecision.DeletePackage
                && !reportForm.DeleteConfirmation)
            {
                ModelState.AddModelError(
                    nameof(ReportMyPackageViewModel.DeleteConfirmation),
                    Strings.UserPackageDeleteConfirmationIsRequired);
            }

            // Unless we're performing an auto-delete, require a message.
            if (reportForm.DeleteDecision != PackageDeleteDecision.DeletePackage
                && string.IsNullOrWhiteSpace(reportForm.Message))
            {
                ModelState.AddModelError(
                    nameof(ReportMyPackageViewModel.Message),
                    Strings.MessageIsRequired);
            }

            if (!ModelState.IsValid)
            {
                return await ReportMyPackage(package.PackageRegistration.Id, package.NormalizedVersion);
            }

            return null;
        }

        private void NotifyReportMyPackageSupportRequest(ReportMyPackageViewModel reportForm, Package package, User user, MailAddress from)
        {
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

            _messageService.ReportMyPackage(request);

            TempData["Message"] = Strings.SupportRequestSentTransientMessage;
        }

        private async Task<bool> DeletePackageOnBehalfOfUserAsync(
            Package package,
            User user,
            string reason,
            Issue supportRequest)
        {
            var deleted = false;
            try
            {
                await _packageDeleteService.SoftDeletePackagesAsync(
                    new[] { package },
                    user,
                    reason,
                    signature: Strings.UserPackageDeleteSignature);
                deleted = true;
            }
            catch (Exception e)
            {
                // Swallow exceptions that occur during the delete process. If this happens, we'll just
                // send out the support request as usual and handle it manually.
                QuietLog.LogHandledException(e);
            }

            if (deleted)
            {
                // Only close the support request if we have successfully deleted the package.
                await _supportRequestService.UpdateIssueAsync(
                    issueId: supportRequest.Key,
                    assignedToId: null,
                    issueStatusId: IssueStatusKeys.Resolved,
                    comment: null,
                    editedBy: user.Username);

                _messageService.SendPackageDeletedNotice(
                    package,
                    Url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                    Url.ReportPackage(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false));

                TempData["Message"] = Strings.UserPackageDeleteCompleteTransientMessage;
            }

            return deleted;
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual ActionResult ContactOwners(string id)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version: null);

            if (package == null || package.PackageRegistration == null)
            {
                return HttpNotFound();
            }

            bool hasOwners = package.PackageRegistration.Owners.Any();
            var model = new ContactOwnersViewModel
            {
                PackageId = package.PackageRegistration.Id,
                ProjectUrl = package.ProjectUrl,
                Owners = package.PackageRegistration.Owners.Where(u => u.EmailAllowed),
                CopySender = true,
                HasOwners = hasOwners
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
                Url.Package(package, false),
                contactForm.Message,
                Url.AccountSettings(relativeUrl: false),
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

        [HttpGet]
        [Authorize]
        public virtual ActionResult ManagePackageOwners(string id)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, string.Empty);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new ManagePackageOwnersViewModel(package, GetCurrentUser());

            return View(model);
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("delete a package")]
        public virtual ActionResult Delete(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (ActionsRequiringPermissions.UnlistOrRelistPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DeletePackageViewModel(package, GetCurrentUser(), ReportMyPackageReasons);

            model.VersionSelectList = new SelectList(
                model.PackageVersions
                .Where(p => !p.Deleted)
                .Select(p => new
                {
                    text = p.NuGetVersion.ToFullString() + (p.LatestVersionSemVer2 ? " (Latest)" : string.Empty),
                    url = Url.DeletePackage(p)
                }), "url", "text", Url.DeletePackage(model));

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
                (PackageService)_packageService,
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

                ex.Log();
            }

            return SafeRedirect(Url.Package(id, version));
        }

        [Authorize(Roles = "Admins")]
        [RequiresAccountConfirmation("revalidate a package")]
        public virtual async Task<ActionResult> Revalidate(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            try
            {
                await _validationService.RevalidateAsync(package);

                TempData["Message"] = "The package is being revalidated.";
            }
            catch (Exception ex)
            {
                ex.Log();

                TempData["Message"] = $"An error occurred while revalidating the package. {ex.Message}";
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
                        var packageToDelete = _packageService.FindPackageByIdAndVersionStrict(split[0], split[1]);
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

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("edit a package")]
        public virtual async Task<ActionResult> Edit(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            // Create model from the package.
            var model = new EditPackageRequest
            {
                PackageId = package.PackageRegistration.Id,
                PackageTitle = package.Title,
                Version = package.NormalizedVersion,
                IsLocked = package.PackageRegistration.IsLocked,
            };

            if (!model.IsLocked)
            {
                model.PackageVersions = package.PackageRegistration.Packages
                      .OrderByDescending(p => new NuGetVersion(p.Version), Comparer<NuGetVersion>.Create((a, b) => a.CompareTo(b)))
                      .ToList();

                // Create version selection.
                model.VersionSelectList = new SelectList(model.PackageVersions.Select(e => new
                {
                    text = NuGetVersion.Parse(e.Version).ToFullString() + (e.IsLatestSemVer2 ? " (Latest)" : string.Empty),
                    url = UrlExtensions.EditPackage(Url, model.PackageId, e.NormalizedVersion)
                }), "url", "text", UrlExtensions.EditPackage(Url, model.PackageId, model.Version));

                model.Edit = new EditPackageVersionReadMeRequest();

                // Update edit model with the readme.md data.
                if (package.HasReadMe)
                {
                    model.Edit.ReadMe.SourceType = ReadMeService.TypeWritten;
                    model.Edit.ReadMe.SourceText = await _readMeService.GetReadMeMdAsync(package);
                }
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("edit a package")]
        public virtual async Task<JsonResult> Edit(string id, string version, VerifyPackageRequest formData, string returnUrl)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return Json(404, new[] { string.Format(Strings.PackageWithIdAndVersionNotFound, id, version) });
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return Json(403, new[] { Strings.Unauthorized });
            }

            if (package.PackageRegistration.IsLocked)
            {
                return Json(403, new[] { string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id) });
            }

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(400, errorMessages);
            }

            if (formData.Edit != null)
            {
                // Update readme.md file, if modified.
                var readmeChanged = await _readMeService.SaveReadMeMdIfChanged(package, formData.Edit, Request.ContentEncoding);
                if (readmeChanged)
                {
                    _telemetryService.TrackPackageReadMeChangeEvent(package, formData.Edit.ReadMe.SourceType, formData.Edit.ReadMeState);

                    // Add an auditing record for the package edit.
                    await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.Edit));
                }
            }

            return Json(new
            {
                location = returnUrl ?? Url.Package(id, version)
            });
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("accept ownership of a package")]
        public virtual Task<ActionResult> ConfirmPendingOwnershipRequest(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, accept: true);
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("reject ownership of a package")]
        public virtual Task<ActionResult> RejectPendingOwnershipRequest(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, accept: false);
        }

        private async Task<ActionResult> HandleOwnershipRequest(string id, string username, string token, bool accept)
        {
            if (string.IsNullOrEmpty(token))
            {
                return HttpNotFound();
            }

            var user = _userService.FindByUsername(username);
            if (ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(GetCurrentUser(), user) != PermissionsCheckResult.Allowed)
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.NotYourRequest));
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (package.Owners.Any(o => o.MatchesUser(user)))
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.AlreadyOwner));
            }

            var request = _packageOwnershipManagementService.GetPackageOwnershipRequest(package, user, token);
            if (request == null)
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Failure));
            }

            if (accept)
            {
                var result = await HandleSecurePushPropagation(package, user);

                await _packageOwnershipManagementService.AddPackageOwnerAsync(package, user);

                SendAddPackageOwnerNotification(package, user, result.Item1, result.Item2);

                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Success));
            }
            else
            {
                var requestingUser = request.RequestingOwner;

                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(package, user);

                _messageService.SendPackageOwnerRequestRejectionNotice(requestingUser, user, package);

                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Rejected));
            }
        }

        [HttpGet]
        [Authorize]
        [RequiresAccountConfirmation("cancel pending ownership request")]
        public virtual async Task<ActionResult> CancelPendingOwnershipRequest(string id, string requestingUsername, string pendingUsername)
        {
            if (!string.Equals(requestingUsername, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, requestingUsername, ConfirmOwnershipResult.NotYourRequest));
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var requestingUser = GetCurrentUser();

            var pendingUser = _userService.FindByUsername(pendingUsername);
            if (pendingUser == null)
            {
                return HttpNotFound();
            }

            var request = _packageOwnershipManagementService.GetPackageOwnershipRequests(package, requestingUser, pendingUser).FirstOrDefault();
            if (request == null)
            {
                return HttpNotFound();
            }

            await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(package, pendingUser);

            _messageService.SendPackageOwnerRequestCancellationNotice(requestingUser, pendingUser, package);

            return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, pendingUsername, ConfirmOwnershipResult.Cancelled));
        }

        /// <summary>
        /// Send notification that a new package owner was added.
        /// </summary>
        /// <param name="package">Package to which owner was added.</param>
        /// <param name="newOwner">Owner added.</param>
        /// <param name="propagators">Propagating owners for secure push.</param>
        /// <param name="subscribed">Owners subscribed to secure push.</param>
        private void SendAddPackageOwnerNotification(PackageRegistration package, User newOwner, List<User> propagators, List<User> subscribed)
        {
            var packageUrl = Url.Package(package.Id, version: null, relativeUrl: false);
            Func<User, bool> notNewOwner = o => !o.Username.Equals(newOwner.Username, StringComparison.OrdinalIgnoreCase);

            // prepare policy messages if there were any secure push subscriptions.
            var propagatorsPolicyMessage = string.Empty;
            var subscribedPolicyMessage = string.Empty;
            if (subscribed.Any())
            {
                propagatorsPolicyMessage = string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerNotification_SecurePushRequired_Propagators,
                    string.Join(", ", propagators.Select(u => u.Username)),
                    string.Join(", ", subscribed.Select(s => s.Username)),
                    GetSecurePushPolicyDescriptions(), _config.GalleryOwner.Address);

                subscribedPolicyMessage = string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerNotification_SecurePushRequired_Subscribed,
                    string.Join(", ", propagators.Select(u => u.Username)),
                    GetSecurePushPolicyDescriptions(), _config.GalleryOwner.Address);
            }
            else
            {
                // new owner should only be notified if they have propagated policies.
                propagators = propagators.Where(notNewOwner).ToList();
            }

            // notify propagators about new owner, including policy statement if any owners were subscribed.
            propagators.ForEach(owner => _messageService.SendPackageOwnerAddedNotice(owner, newOwner, package, packageUrl, propagatorsPolicyMessage));

            // notify subscribed about new owner, including policy statement.
            subscribed.Where(notNewOwner).ToList()
                .ForEach(owner => _messageService.SendPackageOwnerAddedNotice(owner, newOwner, package, packageUrl, subscribedPolicyMessage));

            // notify already subscribed about new owner, excluding any policy statement.
            var notSubscribed = package.Owners.Where(notNewOwner).Except(propagators).Except(subscribed).ToList();
            notSubscribed.ForEach(owner => _messageService.SendPackageOwnerAddedNotice(owner, newOwner, package, packageUrl, string.Empty));
        }

        /// <summary>
        /// Enforce secure push policies on co-owners if new or existing owner requires it.
        /// </summary>
        /// <returns>Tuple where Item1 is propagators, Item2 is subscribed owners</returns>
        private async Task<Tuple<List<User>, List<User>>> HandleSecurePushPropagation(PackageRegistration package, User user)
        {
            var subscribed = new List<User>();
            var propagators = package.Owners.Where(RequireSecurePushForCoOwnersPolicy.IsSubscribed).ToList();

            if (RequireSecurePushForCoOwnersPolicy.IsSubscribed(user))
            {
                propagators.Add(user);
            }

            if (propagators.Any())
            {
                if (await SubscribeToSecurePushAsync(user))
                {
                    subscribed.Add(user);
                }
                foreach (var owner in package.Owners)
                {
                    if (await SubscribeToSecurePushAsync(owner))
                    {
                        subscribed.Add(owner);
                    }
                }
            }

            return Tuple.Create(propagators, subscribed);
        }

        private string GetSecurePushPolicyDescriptions()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SecurePushPolicyDescriptions,
                SecurePushSubscription.MinProtocolVersion, SecurePushSubscription.PushKeysExpirationInDays);
        }

        private async Task<bool> SubscribeToSecurePushAsync(User user)
        {
            try
            {
                return await _securityPolicyService.SubscribeAsync(user, SecurePushSubscription.Name);
            }
            catch (Exception ex)
            {
                ex.Log();

                throw;
            }
        }

        internal virtual async Task<ActionResult> Edit(string id, string version, bool? listed, Func<Package, bool, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            if (package.PackageRegistration.IsLocked)
            {
                return new HttpStatusCodeResult(403, string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id));
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
            return Redirect(urlFactory(package, /*relativeUrl:*/ true));
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("upload a package")]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        public virtual async Task<JsonResult> VerifyPackage(VerifyPackageRequest formData)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(400, errorMessages);
            }

            var currentUser = GetCurrentUser();

            // Check that the owner specified in the form is valid
            var owner = _userService.FindByUsername(formData.Owner);

            if (owner == null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.VerifyPackage_UserNonExistent, formData.Owner);
                return Json(400, new[] { message });
            }

            Package package;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return Json(400, new[] { Strings.VerifyPackage_UploadNotFound });
                }

                var nugetPackage = await SafeCreatePackage(currentUser, uploadFile);
                if (nugetPackage == null)
                {
                    // Send the user back
                    return Json(400, new[] { Strings.VerifyPackage_UnexpectedError });
                }

                Debug.Assert(nugetPackage != null);

                var packageMetadata = PackageMetadata.FromNuspecReader(
                    nugetPackage.GetNuspecReader());

                // Rule out problem scenario with multiple tabs - verification request (possibly with edits) was submitted by user
                // viewing a different package to what was actually most recently uploaded
                if (!(String.IsNullOrEmpty(formData.Id) || String.IsNullOrEmpty(formData.OriginalVersion)))
                {
                    if (!(String.Equals(packageMetadata.Id, formData.Id, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(packageMetadata.Version.ToFullStringSafe(), formData.Version, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(packageMetadata.Version.OriginalVersion, formData.OriginalVersion, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Json(400, new[] { Strings.VerifyPackage_PackageFileModified });
                    }
                }

                var packageStreamMetadata = new PackageStreamMetadata
                {
                    HashAlgorithm = Constants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(uploadFile.AsSeekableStream()),
                    Size = uploadFile.Length,
                };

                var packageId = packageMetadata.Id;
                var packageVersion = packageMetadata.Version;

                var existingPackageRegistration = _packageService.FindPackageRegistrationById(packageId);
                if (existingPackageRegistration == null)
                {
                    var checkPermissionsOfUploadNewId = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(currentUser, owner, new ActionOnNewPackageContext(packageId, _reservedNamespaceService));
                    if (checkPermissionsOfUploadNewId != PermissionsCheckResult.Allowed)
                    {
                        if (checkPermissionsOfUploadNewId == PermissionsCheckResult.AccountFailure)
                        {
                            // The user is not allowed to upload a new ID on behalf of the owner specified in the form
                            var message = string.Format(CultureInfo.CurrentCulture,
                                Strings.UploadPackage_NewIdOnBehalfOfUserNotAllowed,
                                currentUser.Username, owner.Username);
                            return Json(400, new[] { message });
                        }
                        else if (checkPermissionsOfUploadNewId == PermissionsCheckResult.ReservedNamespaceFailure)
                        {
                            // The owner specified in the form is not allowed to push to a reserved namespace matching the new ID
                            var version = packageVersion.ToNormalizedString();
                            _telemetryService.TrackPackagePushNamespaceConflictEvent(packageId, version, currentUser, User.Identity);

                            var message = string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict);
                            return Json(409, new string[] { message });
                        }

                        // An unknown error occurred.
                        return Json(400, new[] { Strings.VerifyPackage_UnexpectedError });
                    }
                }
                else
                {
                    var checkPermissionsOfUploadNewVersion = ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissions(currentUser, owner, existingPackageRegistration);
                    if (checkPermissionsOfUploadNewVersion != PermissionsCheckResult.Allowed)
                    {
                        if (checkPermissionsOfUploadNewVersion == PermissionsCheckResult.AccountFailure)
                        {
                            // The user is not allowed to upload a new version on behalf of the owner specified in the form
                            var message = string.Format(CultureInfo.CurrentCulture,
                                Strings.UploadPackage_NewVersionOnBehalfOfUserNotAllowed,
                                currentUser.Username, owner.Username);
                            return Json(400, new[] { message });
                        }

                        if (checkPermissionsOfUploadNewVersion == PermissionsCheckResult.PackageRegistrationFailure)
                        {
                            // The owner specified in the form is not allowed to upload a new version of the package
                            var message = string.Format(CultureInfo.CurrentCulture,
                                Strings.VerifyPackage_OwnerInvalid,
                                owner.Username, existingPackageRegistration.Id);
                            return Json(400, new[] { message });
                        }

                        // An unknown error occurred.
                        return Json(400, new[] { Strings.VerifyPackage_UnexpectedError });
                    }
                }

                // update relevant database tables
                try
                {
                    package = await _packageUploadService.GeneratePackageAsync(
                        packageMetadata.Id,
                        nugetPackage,
                        packageStreamMetadata,
                        owner,
                        currentUser);

                    Debug.Assert(package.PackageRegistration != null);
                }
                catch (InvalidPackageException ex)
                {
                    _telemetryService.TraceException(ex);

                    return Json(400, new[] { ex.Message });
                }

                if (formData.Edit != null)
                {
                    if (await _readMeService.SaveReadMeMdIfChanged(package, formData.Edit, Request.ContentEncoding))
                    {
                        _telemetryService.TrackPackageReadMeChangeEvent(package, formData.Edit.ReadMe.SourceType, formData.Edit.ReadMeState);
                    }
                }

                await _packageService.PublishPackageAsync(package, commitChanges: false);

                if (!formData.Listed)
                {
                    await _packageService.MarkPackageUnlistedAsync(package, commitChanges: false);
                }

                await _autoCuratedPackageCmd.ExecuteAsync(package, nugetPackage, commitChanges: false);

                // Commit the package to storage and to the database.
                uploadFile.Position = 0;
                var commitResult = await _packageUploadService.CommitPackageAsync(
                    package,
                    uploadFile.AsSeekableStream());

                switch (commitResult)
                {
                    case PackageCommitResult.Success:
                        break;
                    case PackageCommitResult.Conflict:
                        TempData["Message"] = Strings.UploadPackage_IdVersionConflict;
                        return Json(409, new[] { Strings.UploadPackage_IdVersionConflict });
                    default:
                        throw new NotImplementedException($"The package commit result {commitResult} is not supported.");
                }

                // tell Lucene to update index for the new package
                _indexingService.UpdateIndex();

                // write an audit record
                await _auditingService.SaveAuditRecordAsync(
                    new PackageAuditRecord(package, AuditedPackageAction.Create, PackageCreatedVia.Web));

                if (!(_config.AsynchronousPackageValidationEnabled && _config.BlockingAsynchronousPackageValidationEnabled))
                {
                    // notify user unless async validation in blocking mode is used
                    _messageService.SendPackageAddedNotice(package,
                        Url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                        Url.ReportPackage(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                        Url.AccountSettings(relativeUrl: false));
                }
            }

            // delete the uploaded binary in the Uploads container
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            _telemetryService.TrackPackagePushEvent(package, currentUser, User.Identity);

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture, Strings.SuccessfullyUploadedPackage, package.PackageRegistration.Id, package.Version);

            return Json(new
            {
                location = Url.Package(package.PackageRegistration.Id, package.NormalizedVersion)
            });
        }

        private async Task<PackageArchiveReader> SafeCreatePackage(User currentUser, Stream uploadFile)
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
        public virtual async Task<JsonResult> CancelUpload()
        {
            var currentUser = GetCurrentUser();
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            return Json(null);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> PreviewReadMe(ReadMeRequest formData)
        {
            if (formData == null || !_readMeService.HasReadMeSource(formData))
            {
                return Json(400, new[] { Strings.PreviewReadMe_ReadMeMissing });
            }

            try
            {
                var readMeHtml = await _readMeService.GetReadMeHtmlAsync(formData, Request.ContentEncoding);
                return Json(new[] { readMeHtml });
            }
            catch (Exception ex)
            {
                return Json(400, new[] { string.Format(CultureInfo.CurrentCulture, Strings.PreviewReadMe_ConversionFailed, ex.Message) });
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> SetLicenseReportVisibility(string id, string version, bool visible)
        {
            return await SetLicenseReportVisibility(id, version, visible, Url.Package);
        }

        internal virtual async Task<ActionResult> SetLicenseReportVisibility(string id, string version, bool visible, Func<Package, bool, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
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

            return Redirect(urlFactory(package, /*relativeUrl:*/ true));
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