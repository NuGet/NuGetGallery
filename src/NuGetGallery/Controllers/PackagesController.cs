// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Infrastructure.Mail.Requests;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.OData;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.ViewModels;

namespace NuGetGallery
{
    public partial class PackagesController
        : AppController
    {
        /// <summary>
        /// The upper limit on allowed license file size for displaying in gallery.
        /// </summary>
        /// <remarks>
        /// Warning: This limit should never be decreased! And this limit must be less than int.MaxValue!
        /// <see cref="MaxAllowedLicenseLengthForUploading"/> in <see cref="PackageUploadService"/> is used to limit the license file size during uploading.
        /// </remarks>
        internal const int MaxAllowedLicenseLengthForDisplaying = 1024 * 1024; // 1 MB

        /// <summary>
        /// Only perform the "is indexed" check for a short time after the package was lasted edited or created.
        /// </summary>
        private static readonly TimeSpan IsIndexedCheckUntil = TimeSpan.FromDays(1);

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

        private static readonly IReadOnlyCollection<string> AllowedPackageExtentions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoreConstants.NuGetPackageFileExtension,
            CoreConstants.NuGetSymbolPackageFileExtension
        };

        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        private readonly IAppConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IPackageUpdateService _packageUpdateService;
        private readonly IPackageFileService _packageFileService;
        private readonly ISearchServiceFactory _searchServiceFactory;
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
        private readonly IContentObjectService _contentObjectService;
        private readonly ISymbolPackageUploadService _symbolPackageUploadService;
        private readonly IDiagnosticsSource _trace;
        private readonly ICoreLicenseFileService _coreLicenseFileService;
        private readonly ILicenseExpressionSplitter _licenseExpressionSplitter;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageDeprecationService _deprecationService;
        private readonly IABTestService _abTestService;
        private readonly IIconUrlProvider _iconUrlProvider;
        private readonly DisplayPackageViewModelFactory _displayPackageViewModelFactory;
        private readonly DisplayLicenseViewModelFactory _displayLicenseViewModelFactory;
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelFactory;
        private readonly ManagePackageViewModelFactory _managePackageViewModelFactory;
        private readonly DeletePackageViewModelFactory _deletePackageViewModelFactory;

        public PackagesController(
            IPackageService packageService,
            IPackageUpdateService packageUpdateService,
            IUploadFileService uploadFileService,
            IUserService userService,
            IMessageService messageService,
            ISearchServiceFactory searchServiceFactory,
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
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IContentObjectService contentObjectService,
            ISymbolPackageUploadService symbolPackageUploadService,
            IDiagnosticsService diagnosticsService,
            ICoreLicenseFileService coreLicenseFileService,
            ILicenseExpressionSplitter licenseExpressionSplitter,
            IFeatureFlagService featureFlagService,
            IPackageDeprecationService deprecationService,
            IABTestService abTestService,
            IIconUrlProvider iconUrlProvider)
        {
            _packageService = packageService;
            _packageUpdateService = packageUpdateService ?? throw new ArgumentNullException(nameof(packageUpdateService));
            _uploadFileService = uploadFileService;
            _userService = userService;
            _messageService = messageService;
            _searchServiceFactory = searchServiceFactory ?? throw new ArgumentNullException(nameof(searchServiceFactory));
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
            _contentObjectService = contentObjectService;
            _symbolPackageUploadService = symbolPackageUploadService;
            _trace = diagnosticsService?.SafeGetSource(nameof(PackagesController)) ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            _licenseExpressionSplitter = licenseExpressionSplitter ?? throw new ArgumentNullException(nameof(licenseExpressionSplitter));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _deprecationService = deprecationService ?? throw new ArgumentNullException(nameof(deprecationService));
            _abTestService = abTestService ?? throw new ArgumentNullException(nameof(abTestService));
            _iconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));

            _displayPackageViewModelFactory = new DisplayPackageViewModelFactory(_iconUrlProvider);
            _displayLicenseViewModelFactory = new DisplayLicenseViewModelFactory(_iconUrlProvider);
            _listPackageItemViewModelFactory = new ListPackageItemViewModelFactory(_iconUrlProvider);
            _managePackageViewModelFactory = new ManagePackageViewModelFactory(_iconUrlProvider);
            _deletePackageViewModelFactory = new DeletePackageViewModelFactory(_iconUrlProvider);
        }

        [HttpGet]
        [UIAuthorize]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public virtual JsonResult UploadPackageProgress()
        {
            string username = User.Identity.Name;
            string uploadTracingKey = UploadHelper.GetUploadTracingKey(Request.Headers);

            var uploadKey = username + uploadTracingKey;

            AsyncFileUploadProgress progress = _cacheService.GetProgress(uploadKey);
            if (progress == null)
            {
                return Json(HttpStatusCode.NotFound, null, JsonRequestBehavior.AllowGet);
            }

            return Json(progress, JsonRequestBehavior.AllowGet);
        }

        [UIAuthorize]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<ActionResult> UploadPackage()
        {
            var currentUser = GetCurrentUser();
            var model = new SubmitPackageRequest();
            model.IsSymbolsUploadEnabled = _contentObjectService.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(currentUser);
            PackageMetadata packageMetadata;

            using (var uploadedFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadedFile != null)
                {
                    var packageArchiveReader = await SafeCreatePackage(currentUser, uploadedFile);
                    if (packageArchiveReader == null)
                    {
                        return View(model);
                    }

                    try
                    {
                        packageMetadata = PackageMetadata.FromNuspecReader(
                            packageArchiveReader.GetNuspecReader(),
                            strict: true);
                    }
                    catch (Exception ex)
                    {
                        _telemetryService.TraceException(ex);

                        TempData["Message"] = ex.GetUserSafeMessage();
                        return View(model);
                    }

                    if (packageMetadata.IsSymbolsPackage())
                    {
                        return await UploadSymbolsPackageInternal(model, uploadedFile, packageArchiveReader, packageMetadata, currentUser);
                    }
                    else
                    {
                        return await UploadPackageInternal(model, packageArchiveReader, packageMetadata, currentUser);
                    }
                }
            }

            return View(model);
        }

        private async Task<ActionResult> UploadSymbolsPackageInternal(SubmitPackageRequest model,
            Stream uploadStream,
            PackageArchiveReader packageArchiveReader,
            PackageMetadata packageMetadata,
            User currentUser)
        {
            var symbolsPackageValidationResult = await _symbolPackageUploadService.ValidateUploadedSymbolsPackage(uploadStream, currentUser);
            var uploadResult = GetJsonResultOrNull(symbolsPackageValidationResult);
            if (uploadResult != null)
            {
                TempData["Message"] = symbolsPackageValidationResult.Message;
                return View(model);
            }

            var packageForUploadingSymbols = symbolsPackageValidationResult.Package;
            var existingPackageRegistration = packageForUploadingSymbols.PackageRegistration;

            IEnumerable<User> accountsAllowedOnBehalfOf = Enumerable.Empty<User>();
            bool isAllowed = ActionsRequiringPermissions.UploadSymbolPackage.CheckPermissionsOnBehalfOfAnyAccount(currentUser, existingPackageRegistration, out accountsAllowedOnBehalfOf) == PermissionsCheckResult.Allowed;
            if (!isAllowed)
            {
                accountsAllowedOnBehalfOf = new[] { currentUser };
            }

            var verifyRequest = new VerifyPackageRequest(packageMetadata, accountsAllowedOnBehalfOf, existingPackageRegistration);
            verifyRequest.IsSymbolsPackage = true;
            verifyRequest.HasExistingAvailableSymbols = packageForUploadingSymbols.IsLatestSymbolPackageAvailable();

            model.InProgressUpload = verifyRequest;

            return View(model);
        }

        private async Task<ActionResult> UploadPackageInternal(SubmitPackageRequest model,
            PackageArchiveReader packageArchiveReader,
            PackageMetadata packageMetadata,
            User currentUser)
        {
            var validationResult = await _packageUploadService.ValidateBeforeGeneratePackageAsync(packageArchiveReader, packageMetadata, currentUser);
            var validationErrorMessage = GetErrorMessageOrNull(validationResult);
            if (validationErrorMessage != null)
            {
                TempData["Message"] = validationErrorMessage.PlainTextMessage;
                return View(model);
            }

            var existingPackageRegistration = _packageService.FindPackageRegistrationById(packageMetadata.Id);
            bool isAllowed;
            IEnumerable<User> accountsAllowedOnBehalfOf = Enumerable.Empty<User>();
            if (existingPackageRegistration == null)
            {
                isAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissionsOnBehalfOfAnyAccount(
                        currentUser, new ActionOnNewPackageContext(packageMetadata.Id, _reservedNamespaceService), out accountsAllowedOnBehalfOf) == PermissionsCheckResult.Allowed;
            }
            else
            {
                isAllowed = ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissionsOnBehalfOfAnyAccount(
                        currentUser, existingPackageRegistration, out accountsAllowedOnBehalfOf) == PermissionsCheckResult.Allowed;
            }

            if (!isAllowed)
            {
                // If the current user cannot upload the package on behalf of any of the existing owners, show the current user as the only possible owner in the upload form.
                // The package upload will be rejected by submitting the form.
                // Related: https://github.com/NuGet/NuGetGallery/issues/5043
                accountsAllowedOnBehalfOf = new[] { currentUser };
            }

            var verifyRequest = new VerifyPackageRequest(packageMetadata, accountsAllowedOnBehalfOf, existingPackageRegistration);
            verifyRequest.Warnings.AddRange(validationResult.Warnings.Select(w => new JsonValidationMessage(w)));
            verifyRequest.IsSymbolsPackage = false;
            verifyRequest.LicenseFileContents = await GetLicenseFileContentsOrNullAsync(packageMetadata, packageArchiveReader);
            verifyRequest.LicenseExpressionSegments = GetLicenseExpressionSegmentsOrNull(packageMetadata.LicenseMetadata);
            model.InProgressUpload = verifyRequest;
            return View(model);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<JsonResult> UploadPackage(HttpPostedFileBase uploadFile)
        {
            var currentUser = GetCurrentUser();

            string uploadTracingKey = UploadHelper.GetUploadTracingKey(Request.Headers);

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return Json(HttpStatusCode.Conflict, new[] { new JsonValidationMessage(Strings.UploadPackage_UploadInProgress) });
                }
            }

            if (uploadFile == null)
            {
                return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.UploadFileIsRequired) });
            }

            if (!AllowedPackageExtentions.Contains(Path.GetExtension(uploadFile.FileName)))
            {
                return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.UploadFileMustBeNuGetPackage) });
            }

            using (var uploadStream = uploadFile.InputStream)
            {
                try
                {
                    PackageArchiveReader packageArchiveReader = CreatePackage(uploadStream);
                    NuspecReader nuspec;
                    PackageMetadata packageMetadata;
                    var errors = ManifestValidator.Validate(packageArchiveReader.GetNuspec(), out nuspec, out packageMetadata).ToArray();
                    if (errors.Length > 0)
                    {
                        var errorStrings = new List<JsonValidationMessage>();
                        foreach (var error in errors)
                        {
                            errorStrings.Add(new JsonValidationMessage(error.ErrorMessage));
                        }

                        return Json(HttpStatusCode.BadRequest, errorStrings.ToArray());
                    }

                    if (packageMetadata.IsSymbolsPackage())
                    {
                        return await UploadSymbolsPackageInternal(packageArchiveReader, uploadStream, nuspec, packageMetadata);
                    }
                    else
                    {
                        return await UploadPackageInternal(packageArchiveReader, uploadStream, nuspec, packageMetadata);
                    }
                }
                catch (Exception ex)
                {
                    return FailedToReadFile(ex);
                }
                finally
                {
                    var username = currentUser.Username;
                    var uploadKey = username + uploadTracingKey;
                    _cacheService.RemoveProgress(uploadKey);
                }
            }
        }

        private async Task<JsonResult> UploadSymbolsPackageInternal(PackageArchiveReader packageArchiveReader, Stream uploadStream, NuspecReader nuspec, PackageMetadata packageMetadata)
        {
            var currentUser = GetCurrentUser();

            IEnumerable<User> accountsAllowedOnBehalfOf = new[] { currentUser };

            var symbolsPackageValidationResult = await _symbolPackageUploadService.ValidateUploadedSymbolsPackage(uploadStream, currentUser);
            var uploadResult = GetJsonResultOrNull(symbolsPackageValidationResult);
            if (uploadResult != null)
            {
                return uploadResult;
            }

            var packageForUploadingSymbols = symbolsPackageValidationResult.Package;
            var existingPackageRegistration = packageForUploadingSymbols.PackageRegistration;

            // Evaluate the permissions for user on behalf of any account possible, since the user 
            // could change the ownership before submitting the package.
            if (ActionsRequiringPermissions.UploadSymbolPackage.CheckPermissionsOnBehalfOfAnyAccount(
                currentUser, existingPackageRegistration, out accountsAllowedOnBehalfOf) != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Conflict, new[] {
                    new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, existingPackageRegistration.Id)) });
            }

            if (existingPackageRegistration.IsLocked)
            {
                return Json(HttpStatusCode.Forbidden, new[] {
                    new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, existingPackageRegistration.Id)) });
            }

            // Save the uploaded file
            await _uploadFileService.SaveUploadFileAsync(currentUser.Key, uploadStream);

            return await GetVerifyPackageView(currentUser,
                packageMetadata,
                accountsAllowedOnBehalfOf,
                existingPackageRegistration,
                isSymbolsPackageUpload: true,
                hasExistingSymbolsPackageAvailable: packageForUploadingSymbols.IsLatestSymbolPackageAvailable());
        }

        private async Task<JsonResult> UploadPackageInternal(PackageArchiveReader packageArchiveReader, Stream uploadStream, NuspecReader nuspec, PackageMetadata packageMetadata)
        {
            var currentUser = GetCurrentUser();

            PackageRegistration existingPackageRegistration;
            // If the current user cannot upload the package on behalf of any of the existing owners, show the current user as the only possible owner in the upload form.
            // If the current user doesn't have the rights to upload the package, the package upload will be rejected by submitting the form.
            // Related: https://github.com/NuGet/NuGetGallery/issues/5043
            IEnumerable<User> accountsAllowedOnBehalfOf = new[] { currentUser };
            var foundEntryInFuture = ZipArchiveHelpers.FoundEntryInFuture(uploadStream, out var entryInTheFuture);
            if (foundEntryInFuture)
            {
                return Json(HttpStatusCode.BadRequest, new[] {
                    new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.PackageEntryFromTheFuture, entryInTheFuture.Name)) });
            }

            try
            {
                await _packageService.EnsureValid(packageArchiveReader);
            }
            catch (Exception ex)
            {
                return FailedToReadFile(ex);
            }

            // Check min client version
            if (nuspec.GetMinClientVersion() > GalleryConstants.MaxSupportedMinClientVersion)
            {
                return Json(HttpStatusCode.BadRequest, new[] {
                    new JsonValidationMessage(
                        string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_MinClientVersionOutOfRange, nuspec.GetMinClientVersion())) });
            }

            var id = nuspec.GetId();
            existingPackageRegistration = _packageService.FindPackageRegistrationById(id);
            // For a new package id verify if the user is allowed to use it.
            if (existingPackageRegistration == null &&
                ActionsRequiringPermissions.UploadNewPackageId.CheckPermissionsOnBehalfOfAnyAccount(
                    currentUser, new ActionOnNewPackageContext(id, _reservedNamespaceService), out accountsAllowedOnBehalfOf) != PermissionsCheckResult.Allowed)
            {
                var version = nuspec.GetVersion().ToNormalizedString();
                _telemetryService.TrackPackagePushNamespaceConflictEvent(id, version, currentUser, User.Identity);

                return Json(HttpStatusCode.Conflict, new[] {
                    new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict)) });
            }

            // For existing package id verify if it is owned by the current user
            if (existingPackageRegistration != null)
            {
                if (ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissionsOnBehalfOfAnyAccount(
                    currentUser, existingPackageRegistration, out accountsAllowedOnBehalfOf) != PermissionsCheckResult.Allowed)
                {
                    return Json(HttpStatusCode.Conflict, new[] {
                        new JsonValidationMessage(
                            string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, existingPackageRegistration.Id)) });
                }

                if (existingPackageRegistration.IsLocked)
                {
                    return Json(HttpStatusCode.Forbidden, new[] {
                        new JsonValidationMessage(
                            string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, existingPackageRegistration.Id)) });
                }
            }

            var nuspecVersion = nuspec.GetVersion();
            var existingPackage = _packageService.FindPackageByIdAndVersionStrict(nuspec.GetId(), nuspecVersion.ToStringSafe());
            if (existingPackage != null)
            {
                if (existingPackage.PackageStatusKey == PackageStatus.FailedValidation)
                {
                    _telemetryService.TrackPackageReupload(existingPackage);

                    // Packages that failed validation can be reuploaded.
                    await _packageDeleteService.HardDeletePackagesAsync(
                        new[] { existingPackage },
                        currentUser,
                        Strings.FailedValidationHardDeleteReason,
                        Strings.AutomatedPackageDeleteSignature,
                        deleteEmptyPackageRegistration: false);
                }
                else
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

                    return Json(HttpStatusCode.Conflict, new[] { new JsonValidationMessage(message) });
                }
            }

            await _uploadFileService.SaveUploadFileAsync(currentUser.Key, uploadStream);

            var hasExistingSymbolsPackageAvailable = existingPackage != null && existingPackage.IsLatestSymbolPackageAvailable();

            return await GetVerifyPackageView(currentUser,
                packageMetadata,
                accountsAllowedOnBehalfOf,
                existingPackageRegistration,
                isSymbolsPackageUpload: false,
                hasExistingSymbolsPackageAvailable: hasExistingSymbolsPackageAvailable);
        }

        private async Task<JsonResult> GetVerifyPackageView(User currentUser,
            PackageMetadata packageMetadata,
            IEnumerable<User> accountsAllowedOnBehalfOf,
            PackageRegistration existingPackageRegistration,
            bool isSymbolsPackageUpload,
            bool hasExistingSymbolsPackageAvailable)
        {
            PackageContentData packageContentData = null;
            try
            {
                packageContentData = await ValidateAndProcessPackageContents(currentUser, isSymbolsPackageUpload);
            }
            catch (Exception)
            {
                await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);
                throw;
            }

            if (packageContentData.ErrorResult != null)
            {
                await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);
                return packageContentData.ErrorResult;
            }

            var model = new VerifyPackageRequest(packageMetadata, accountsAllowedOnBehalfOf, existingPackageRegistration);
            model.IsSymbolsPackage = isSymbolsPackageUpload;
            model.HasExistingAvailableSymbols = hasExistingSymbolsPackageAvailable;
            model.Warnings.AddRange(packageContentData.Warnings.Select(w => new JsonValidationMessage(w)));
            model.LicenseFileContents = packageContentData.LicenseFileContents;
            model.LicenseExpressionSegments = packageContentData.LicenseExpressionSegments;

            if (packageContentData.EmbeddedIconInformation != null)
            {
                model.IconUrl = $"data:{packageContentData.EmbeddedIconInformation.EmbeddedIconContentType};base64,{Convert.ToBase64String(packageContentData.EmbeddedIconInformation.EmbeddedIconData)}";
            }

            return Json(model);
        }

        private class PackageContentData
        {
            public PackageContentData(
                JsonResult errorResult)
            {
                ErrorResult = errorResult;
            }

            public PackageContentData(
                PackageMetadata packageMetadata,
                IReadOnlyList<IValidationMessage> warnings,
                string licenseFileContents,
                IReadOnlyCollection<CompositeLicenseExpressionSegmentViewModel> licenseExpressionSegments,
                EmbeddedIconInformation embeddedIconInformation)
            {
                PackageMetadata = packageMetadata;
                Warnings = warnings;
                LicenseFileContents = licenseFileContents;
                LicenseExpressionSegments = licenseExpressionSegments;
                EmbeddedIconInformation = embeddedIconInformation;
            }

            public JsonResult ErrorResult { get; }
            public PackageMetadata PackageMetadata { get; }
            public IReadOnlyList<IValidationMessage> Warnings { get; }
            public string LicenseFileContents { get; }
            public IReadOnlyCollection<CompositeLicenseExpressionSegmentViewModel> LicenseExpressionSegments { get; }
            public EmbeddedIconInformation EmbeddedIconInformation { get; }
        }

        private class EmbeddedIconInformation
        {
            public EmbeddedIconInformation(
                string embeddedIconContentType,
                byte[] embeddedIconData)
            {
                EmbeddedIconContentType = embeddedIconContentType;
                EmbeddedIconData = embeddedIconData;
            }

            public string EmbeddedIconContentType { get; }
            public byte[] EmbeddedIconData { get; }
        }

        private async Task<PackageContentData> ValidateAndProcessPackageContents(User currentUser, bool isSymbolsPackageUpload)
        {
            IReadOnlyList<IValidationMessage> warnings = new List<IValidationMessage>();
            string licenseFileContents = null;
            IReadOnlyCollection<CompositeLicenseExpressionSegmentViewModel> licenseExpressionSegments = null;
            PackageMetadata packageMetadata = null;
            EmbeddedIconInformation embeddedIconInformation = null;

            using (Stream uploadedFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadedFile == null)
                {
                    return new PackageContentData(
                        Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.UploadFileIsRequired) }));
                }

                var packageArchiveReader = await SafeCreatePackage(currentUser, uploadedFile);
                if (packageArchiveReader == null)
                {
                    return new PackageContentData(
                        Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.UploadFileIsRequired) }));
                }

                try
                {
                    packageMetadata = PackageMetadata.FromNuspecReader(
                        packageArchiveReader.GetNuspecReader(),
                        strict: true);
                }
                catch (Exception ex)
                {
                    _telemetryService.TraceException(ex);

                    return new PackageContentData(
                        Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(ex.GetUserSafeMessage()) }));
                }

                if (!isSymbolsPackageUpload)
                {
                    var validationResult = await _packageUploadService.ValidateBeforeGeneratePackageAsync(packageArchiveReader, packageMetadata, currentUser);
                    var validationJsonResult = GetJsonResultOrNull(validationResult);
                    if (validationJsonResult != null)
                    {
                        return new PackageContentData(validationJsonResult);
                    }

                    warnings = validationResult.Warnings;
                }

                try
                {
                    licenseFileContents = await GetLicenseFileContentsOrNullAsync(packageMetadata, packageArchiveReader);
                    licenseExpressionSegments = GetLicenseExpressionSegmentsOrNull(packageMetadata.LicenseMetadata);
                    embeddedIconInformation = await GetEmbeddedIconOrNullAsync(packageMetadata, packageArchiveReader);
                }
                catch (Exception ex)
                {
                    _telemetryService.TraceException(ex);

                    return new PackageContentData(
                        Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(ex.GetUserSafeMessage()) }));
                }
            }

            return new PackageContentData(packageMetadata, warnings, licenseFileContents, licenseExpressionSegments, embeddedIconInformation);
        }

        private IReadOnlyCollection<CompositeLicenseExpressionSegmentViewModel> GetLicenseExpressionSegmentsOrNull(LicenseMetadata licenseMetadata)
        {
            if (licenseMetadata?.Type != LicenseType.Expression)
            {
                return null;
            }

            return _licenseExpressionSplitter
                .SplitExpression(licenseMetadata.License)
                .Select(s => new CompositeLicenseExpressionSegmentViewModel(s))
                .ToList();
        }

        private static async Task<string> GetLicenseFileContentsOrNullAsync(PackageMetadata packageMetadata, PackageArchiveReader packageArchiveReader)
        {
            if (packageMetadata.LicenseMetadata?.Type != LicenseType.File)
            {
                return null;
            }

            var licenseFilename = FileNameHelper.GetZipEntryPath(packageMetadata.LicenseMetadata.License);
            using (var licenseFileStream = packageArchiveReader.GetStream(licenseFilename))
            using (var streamReader = new StreamReader(licenseFileStream, Encoding.UTF8))
            {
                return await streamReader.ReadToEndAsync();
            }
        }

        private static async Task<EmbeddedIconInformation> GetEmbeddedIconOrNullAsync(PackageMetadata packageMetadata, PackageArchiveReader packageArchiveReader)
        {
            if (string.IsNullOrWhiteSpace(packageMetadata.IconFile))
            {
                return null;
            }

            var iconFilename = FileNameHelper.GetZipEntryPath(packageMetadata.IconFile);
            var imageData = await ReadPackageFile(packageArchiveReader, iconFilename);
            string imageContentType;
            if (imageData.StartsWithJpegHeader())
            {
                imageContentType = CoreConstants.JpegContentType;
            }
            else if (imageData.StartsWithPngHeader())
            {
                imageContentType = CoreConstants.PngContentType;
            }
            else
            {
                // we should never get here: wrong file contents should have been caught during validation 
                throw new InvalidOperationException("The package icon is neither JPEG nor PNG file");
            }

            return new EmbeddedIconInformation(imageContentType, imageData);
        }

        private static async Task<byte[]> ReadPackageFile(PackageArchiveReader packageArchiveReader, string filename)
        {
            using (var packageFileStream = packageArchiveReader.GetStream(filename))
            using (var destination = new MemoryStream())
            {
                await packageFileStream.CopyToAsync(destination);
                return destination.ToArray();
            }
        }

        public virtual async Task<ActionResult> DisplayPackage(string id, string version)
        {
            string normalized = NuGetVersionFormatter.Normalize(version);
            if (!string.Equals(version, normalized))
            {
                // Permanent redirect to the normalized one (to avoid multiple URLs for the same content)
                return RedirectToActionPermanent("DisplayPackage", new { id = id, version = normalized });
            }

            // Load all packages with the ID.
            Package package = null;
            var allVersions = _packageService.FindPackagesById(id, includePackageRegistration: true);

            if (version != null)
            {
                if (version.Equals(GalleryConstants.AbsoluteLatestUrlString, StringComparison.InvariantCultureIgnoreCase))
                {
                    // The user is looking for the absolute latest version and not an exact version.
                    package = allVersions.FirstOrDefault(p => p.IsLatestSemVer2);
                }
                else
                {
                    package = _packageService.FilterExactPackage(allVersions, version);
                }
            }

            if (package == null)
            {
                // If we cannot find the exact version or no version was provided, fall back to the latest version.
                package = _packageService.FilterLatestPackage(allVersions, SemVerLevelKey.SemVer2, allowPrerelease: true);
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

            var readme = await _readMeService.GetReadMeHtmlAsync(package);
            var deprecations = _deprecationService.GetDeprecationsById(id);
            var packageKeyToDeprecation = deprecations
                .GroupBy(d => d.PackageKey)
                .ToDictionary(g => g.Key, g => g.First());

            var model = _displayPackageViewModelFactory.Create(
                package,
                allVersions,
                currentUser,
                packageKeyToDeprecation,
                readme);

            model.ValidatingTooLong = _validationService.IsValidatingTooLong(package);
            model.PackageValidationIssues = _validationService.GetLatestPackageValidationIssues(package);
            model.SymbolsPackageValidationIssues = _validationService.GetLatestPackageValidationIssues(model.LatestSymbolsPackage);
            model.IsCertificatesUIEnabled = _contentObjectService.CertificatesConfiguration?.IsUIEnabledForUser(currentUser) ?? false;
            model.IsAtomFeedEnabled = _featureFlagService.IsPackagesAtomFeedEnabled();
            model.IsPackageDeprecationEnabled = _featureFlagService.IsManageDeprecationEnabled(currentUser, allVersions);

            if(model.IsGitHubUsageEnabled = _featureFlagService.IsGitHubUsageEnabled(currentUser))
            {
                model.GitHubDependenciesInformation = _contentObjectService.GitHubUsageConfiguration.GetPackageInformation(id);
            }

            if (!string.IsNullOrWhiteSpace(package.LicenseExpression))
            {
                try
                {
                    model.LicenseExpressionSegments = _licenseExpressionSplitter.SplitExpression(package.LicenseExpression);
                }
                catch (Exception ex)
                {
                    // Any exception thrown while trying to render license expression beautifully
                    // is not severe enough to break the client experience, view will fall back to
                    // display license url.
                    _telemetryService.TraceException(ex);
                }
            }

            var searchService = _searchServiceFactory.GetService();
            var externalSearchService = searchService as ExternalSearchService;
            if (searchService.ContainsAllVersions && externalSearchService != null)
            {
                // A package can be re-indexed when it is created or edited. Determine the latest of these times.
                var sinceLatestUpsert = package.Created;
                if (package.LastEdited.HasValue && package.LastEdited > sinceLatestUpsert)
                {
                    sinceLatestUpsert = package.LastEdited.Value;
                }

                // If a package has not been created or edited in quite a while, save the cache memory and search
                // service load by not checking the indexed status.
                var isIndexedCheckUntil = sinceLatestUpsert + IsIndexedCheckUntil;
                if (DateTime.UtcNow < isIndexedCheckUntil)
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

                        var expiration = isIndexedCheckUntil;
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
            }

            ViewBag.FacebookAppID = _config.FacebookAppId;
            return View(model);
        }

        [HttpGet]
        public virtual ActionResult AtomFeed(string id, bool prerel = true)
        {
            if (!_featureFlagService.IsPackagesAtomFeedEnabled())
            {
                return HttpNotFound();
            }

            var packageRegistration = _packageService.FindPackageRegistrationById(id);
            if (packageRegistration == null)
            {
                return HttpNotFound();
            }

            IEnumerable<Package> packageVersionsQuery = packageRegistration
                .Packages
                .Where(x => x.Listed && x.PackageStatusKey == PackageStatus.Available)
                .OrderByDescending(p => NuGetVersion.Parse(p.NormalizedVersion));

            if (!prerel)
            {
                packageVersionsQuery = packageVersionsQuery
                    .Where(x => !x.IsPrerelease);
            }

            var packageVersions = packageVersionsQuery.ToList();

            if (packageVersions.Count == 0)
            {
                return HttpNotFound();
            }

            // most recent version for feed title/description
            var newestVersionPackage = packageVersions.First();

            // the last edited or created package is used as the feed timestamp
            var lastUpdatedPackage = packageVersions.Max(x => x.LastEdited ?? x.Created);

            var feed = new SyndicationFeed()
            {
                Id = Url.Package(packageRegistration.Id, version: null, relativeUrl: false),
                Title = SyndicationContent.CreatePlaintextContent($"{_config.Brand} Feed for {packageRegistration.Id}"),
                Description = SyndicationContent.CreatePlaintextContent(newestVersionPackage.Description),
                LastUpdatedTime = lastUpdatedPackage,
                ImageUrl = _iconUrlProvider.GetIconUrl(newestVersionPackage),
            };

            List<SyndicationItem> feedItems = new List<SyndicationItem>();

            List<SyndicationPerson> ownersAsAuthors = new List<SyndicationPerson>();
            foreach (var packageOwner in packageRegistration.Owners)
            {
                ownersAsAuthors.Add(new SyndicationPerson() { Name = packageOwner.Username, Uri = Url.User(packageOwner, relativeUrl: false) });
            }

            foreach (var packageVersion in packageVersions)
            {
                SyndicationItem syndicationItem = new SyndicationItem($"{packageVersion.Id} {packageVersion.Version}",
                                                                      packageVersion.Description,
                                                                      new Uri(Url.Package(packageRegistration.Id, version: packageVersion.Version, relativeUrl: false)));
                syndicationItem.Id = Url.Package(packageRegistration.Id, version: packageVersion.Version, relativeUrl: false);
                syndicationItem.LastUpdatedTime = packageVersion.LastEdited ?? packageVersion.Created;
                syndicationItem.PublishDate = packageVersion.Created;

                syndicationItem.Authors.AddRange(ownersAsAuthors);
                feedItems.Add(syndicationItem);
            }

            feed.Items = feedItems;

            feed.Links.Add(SyndicationLink.CreateSelfLink(
                new Uri(Url.PackageAtomFeed(packageRegistration.Id, relativeUrl: false)),
                "application/atom+xml"));

            feed.Links.Add(SyndicationLink.CreateAlternateLink(
                new Uri(Url.Package(packageRegistration.Id, version: null, relativeUrl: false)),
                "text/html"));

            return new SyndicationAtomActionResult(feed);
        }

        [HttpGet]
        public virtual async Task<ActionResult> License(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments = null;
            string licenseFileContents = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(package.LicenseExpression))
                {
                    licenseExpressionSegments = _licenseExpressionSplitter.SplitExpression(package.LicenseExpression);
                }

                if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                {
                    /// <remarks>
                    /// The "licenseFileStream" below is already in memory, since low level method <see cref="GetFileAsync"/> in <see cref="CloudBlobCoreFileStorageService"/> reads the whole stream from blob storage into memory.
                    /// There is a need to consider refactoring <see cref="CoreLicenseFileService"/> and provide a "GetUnBufferedFileAsync" method in <see cref="CloudBlobCoreFileStorageService"/>.
                    /// In this way, we could read very large stream from blob storage with max size restriction.
                    /// </remarks>
                    using (var licenseFileStream = await _coreLicenseFileService.DownloadLicenseFileAsync(package))
                    using (var licenseFileTrucatedStream = await licenseFileStream.GetTruncatedStreamWithMaxSizeAsync(MaxAllowedLicenseLengthForDisplaying))
                    {
                        if (licenseFileTrucatedStream.IsTruncated)
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The license file exceeds the max limit of {0} to display in Gallery", MaxAllowedLicenseLengthForDisplaying));
                        }
                        else
                        {
                            licenseFileContents = Encoding.UTF8.GetString(licenseFileTrucatedStream.Stream.GetBuffer(), 0, (int)licenseFileTrucatedStream.Stream.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryService.TraceException(ex);
                throw;
            }

            var model = _displayLicenseViewModelFactory.Create(package, licenseExpressionSegments, licenseFileContents);

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

            var isPreviewSearchEnabled = _abTestService.IsPreviewSearchEnabled(GetCurrentUser());
            var searchService = isPreviewSearchEnabled ? _searchServiceFactory.GetPreviewService() : _searchServiceFactory.GetService();

            // fetch most common query from cache to relieve load on the search service
            if (string.IsNullOrEmpty(q) && page == 1 && includePrerelease)
            {
                var cacheKey = isPreviewSearchEnabled ? "DefaultPreviewSearchResults" : "DefaultSearchResults";
                var cachedResults = HttpContext.Cache.Get(cacheKey);
                if (cachedResults == null)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(
                        q,
                        page,
                        includePrerelease: includePrerelease,
                        sortOrder: null,
                        context: SearchFilter.UISearchContext,
                        semVerLevel: SemVerLevelKey.SemVerLevel2);

                    results = await searchService.Search(searchFilter);

                    // note: this is a per instance cache
                    HttpContext.Cache.Add(
                        cacheKey,
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

                results = await searchService.Search(searchFilter);
            }

            int totalHits = results.Hits;
            if (page == 1 && !results.Data.Any())
            {
                // In the event the index wasn't updated, we may get an incorrect count.
                totalHits = 0;
            }

            var currentUser = GetCurrentUser();
            var items = results.Data
                .Select(pv => _listPackageItemViewModelFactory.Create(pv, currentUser))
                .ToList();

            var viewModel = new PackageListViewModel(
                items,
                results.IndexTimestampUtc,
                q,
                totalHits,
                page - 1,
                GalleryConstants.DefaultPackageListPageSize,
                Url,
                includePrerelease,
                isPreviewSearchEnabled);

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

            ViewData[GalleryConstants.ReturnUrlViewDataKey] = Url.ReportPackage(package);
            return View(model);
        }

        [HttpGet]
        [UIAuthorize]
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

            var allowDelete = await _packageDeleteService.CanPackageBeDeletedByUserAsync(
                package,
                reportPackageReason: null,
                packageDeleteDecision: null);

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
        [ValidateRecaptchaResponse]
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

            var subject = $"Support Request for '{package.PackageRegistration.Id}' version {package.Version}";
            var requestorEmailAddress = user != null ? user.EmailAddress : reportForm.Email;
            var reason = EnumHelper.GetDescription(reportForm.Reason.Value);

            await _supportRequestService.AddNewSupportRequestAsync(subject, reportForm.Message, requestorEmailAddress, reason, user, package);

            var request = new ReportPackageRequest
            {
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                CopySender = reportForm.CopySender,
                Signature = reportForm.Signature,
                PackageUrl = Url.Package(package.PackageRegistration.Id, version: null, relativeUrl: false),
                PackageVersionUrl = Url.Package(package.PackageRegistration.Id, package.Version, relativeUrl: false),
                RequestingUserUrl = user != null ? Url.User(user, relativeUrl: false) : null
            };

            var reportAbuseMessage = new ReportAbuseMessage(
                _config,
                request,
                reportForm.AlreadyContactedOwner);
            await _messageService.SendMessageAsync(reportAbuseMessage);

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";

            return Redirect(Url.Package(id, version));
        }

        [HttpPost]
        [UIAuthorize]
        [RequiresAccountConfirmation("contact support about your package")]
        [ValidateAntiForgeryToken]
        [ValidateRecaptchaResponse]
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

                _telemetryService.TrackUserPackageDeleteExecuted(
                    package.Key,
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    reportForm.Reason.Value,
                    deleted);
            }

            if (!deleted)
            {
                await NotifyReportMyPackageSupportRequestAsync(reportForm, package, user, from);
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
            var allowDelete = await _packageDeleteService.CanPackageBeDeletedByUserAsync(
                package,
                reportForm.Reason,
                reportForm.DeleteDecision);
            if (reportForm.DeleteDecision != PackageDeleteDecision.ContactSupport
                && !allowDelete)
            {
                reportForm.DeleteDecision = null;
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

        private async Task NotifyReportMyPackageSupportRequestAsync(ReportMyPackageViewModel reportForm, Package package, User user, MailAddress from)
        {
            var request = new ReportPackageRequest
            {
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                CopySender = reportForm.CopySender,
                PackageUrl = Url.Package(package.PackageRegistration.Id, version: null, relativeUrl: false),
                PackageVersionUrl = Url.Package(package.PackageRegistration.Id, package.Version, relativeUrl: false),
                RequestingUserUrl = user != null ? Url.User(user, relativeUrl: false) : null
            };

            var reportMyPackageMessage = new ReportMyPackageMessage(_config, request);

            await _messageService.SendMessageAsync(reportMyPackageMessage);

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
                    signature: Strings.AutomatedPackageDeleteSignature);
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

                var emailMessage = new PackageDeletedNoticeMessage(
                    _config,
                    package,
                    Url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                    Url.ReportPackage(package, relativeUrl: false));
                await _messageService.SendMessageAsync(emailMessage);

                TempData["Message"] = Strings.UserPackageDeleteCompleteTransientMessage;
            }

            return deleted;
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual ActionResult ContactOwners(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null || package.PackageRegistration == null)
            {
                return HttpNotFound();
            }

            bool hasOwners = package.PackageRegistration.Owners.Any();
            var model = new ContactOwnersViewModel
            {
                PackageId = id,
                PackageVersion = package.Version,
                ProjectUrl = package.ProjectUrl,
                Owners = package.PackageRegistration.Owners.Where(u => u.EmailAllowed).Select(u => u.Username),
                CopySender = true,
                HasOwners = hasOwners
            };

            return View(model);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [ValidateRecaptchaResponse]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual async Task<ActionResult> ContactOwners(string id, string version, ContactOwnersViewModel contactForm)
        {
            if (!ModelState.IsValid)
            {
                return ContactOwners(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();

            var contactOwnersMessage = new ContactOwnersMessage(
                _config,
                new MailAddress(user.EmailAddress, user.Username),
                package,
                Url.Package(package, false),
                HttpUtility.HtmlEncode(contactForm.Message),
                Url.AccountSettings(relativeUrl: false));

            await _messageService.SendMessageAsync(contactOwnersMessage, contactForm.CopySender, discloseSenderAddress: false);

            string message = string.Format(CultureInfo.CurrentCulture, "Your message has been sent to the owners of {0}.", id);
            TempData["Message"] = message;
            return RedirectToAction(
                actionName: "DisplayPackage",
                controllerName: "Packages",
                routeValues: new
                {
                    id,
                    version
                });
        }

        /// <summary>
        /// This is a redirect for a legacy route.
        /// The <see cref="ManagePackageOwners(string)"/> page was merged with <see cref="Delete(string, string)"/> and <see cref="Edit(string, string)"/> and is now a part of the <see cref="Manage(string, string)"/> page.
        /// </summary>
        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult ManagePackageOwners(string id)
        {
            return RedirectToActionPermanent(nameof(Manage));
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("manage a package")]
        public virtual async Task<ActionResult> Manage(string id, string version = null)
        {
            Package package = null;

            // Load all versions of the package.
            var packages = _packageService.FindPackagesById(
                id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships);

            if (version != null)
            {
                // Try to find the exact version if it was specified.
                package = _packageService.FilterExactPackage(packages, version);
            }

            if (package == null)
            {
                // If the exact version was not found, fall back to the latest version.
                package = _packageService.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, allowPrerelease: true);
            }

            if (package == null)
            {
                // If the package has no versions, return not found.
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var model = _managePackageViewModelFactory.Create(
                package,
                GetCurrentUser(),
                ReportMyPackageReasons,
                Url,
                await _readMeService.GetReadMeMdAsync(package),
                _featureFlagService.IsManageDeprecationEnabled(currentUser, package.PackageRegistration));

            if (!model.CanEdit && !model.CanManageOwners && !model.CanUnlistOrRelist)
            {
                return HttpForbidden();
            }

            return View(model);
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("delete a symbols package")]
        public virtual ActionResult DeleteSymbols(string id, string version)
        {
            Package package = null;

            // Load all versions of the package.
            var packages = _packageService.FindPackagesById(id);
            if (version != null)
            {
                // Try to find the exact version if it was specified.
                package = _packageService.FilterExactPackage(packages, version);
            }

            if (package == null)
            {
                // If the exact version was not found, fall back to the latest version.
                package = _packageService.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, allowPrerelease: true);
            }

            if (package == null)
            {
                // If the package has no versions, return not found.
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            if (ActionsRequiringPermissions.DeleteSymbolPackage.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) != PermissionsCheckResult.Allowed)
            {
                return HttpForbidden();
            }

            var model = _deletePackageViewModelFactory.Create(package, packages, currentUser, DeleteReasons);

            // Fetch all versions of the package with symbols.
            var versionsWithSymbols = packages
                .Where(p => p.PackageStatusKey != PackageStatus.Deleted)
                .Where(p => (p.LatestSymbolPackage()?.StatusKey ?? PackageStatus.Deleted) == PackageStatus.Available)
                .OrderByDescending(p => new NuGetVersion(p.Version));

            model.VersionSelectList = versionsWithSymbols
                .Select(versionWithSymbols => new SelectListItem
                {
                    Text = PackageHelper.GetSelectListText(versionWithSymbols),
                    Value = Url.DeleteSymbolsPackage(new TrivialPackageVersionModel(versionWithSymbols)),
                    Selected = package == versionWithSymbols
                }).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(Roles = "Admins")]
        [RequiresAccountConfirmation("reflow a package")]
        public virtual async Task<ActionResult> Reflow(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var reflowPackageService = new ReflowPackageService(
                _entitiesContext,
                (PackageService)_packageService,
                _packageFileService,
                _telemetryService);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(Roles = "Admins")]
        [RequiresAccountConfirmation("revalidate a package")]
        public virtual async Task<ActionResult> Revalidate(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(Roles = "Admins")]
        [RequiresAccountConfirmation("revalidate a symbols package")]
        public virtual async Task<ActionResult> RevalidateSymbols(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

            if (package == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound,
                    string.Format(Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            try
            {
                // Select the latest symbols package for re-validation only if it is
                // 1. Available
                // 2. Or Pending validations
                // 2. Or Failed Validations
                var latestSymbolPackage = package.LatestSymbolPackage();
                if (latestSymbolPackage == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest,
                        string.Format(Strings.SymbolsPackage_PackageNotAvailable, id, version));
                }

                switch (latestSymbolPackage.StatusKey)
                {
                    case PackageStatus.Available:
                    case PackageStatus.Validating:
                    case PackageStatus.FailedValidation:
                        // Allowed to revalidate
                        break;
                    case PackageStatus.Deleted:
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest,
                            string.Format(Strings.SymbolsPackage_RevalidateDeletedPackage, id, version));
                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest, $"Unkown Package status {latestSymbolPackage.StatusKey}!");
                }

                await _validationService.RevalidateAsync(latestSymbolPackage);

                TempData["Message"] = "The symbols package is being revalidated.";
            }
            catch (Exception ex)
            {
                ex.Log();

                TempData["Message"] = $"An error occurred while revalidating the symbols package. {ex.Message}";
            }

            return SafeRedirect(Url.Package(id, version));
        }

        /// <summary>
        /// This is a redirect for a legacy route.
        /// The <see cref="Delete(string, string)"/> page was merged with <see cref="Edit(string, string)"/> and <see cref="ManagePackageOwners(string)"/> and is now a part of the <see cref="Manage(string, string)"/> page.
        /// </summary>
        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("delete a package")]
        public virtual ActionResult Delete(string id, string version)
        {
            return RedirectToActionPermanent(nameof(Manage));
        }

        [UIAuthorize(Roles = "Admins")]
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
            return await Manage(firstPackage.PackageRegistration.Id, firstPackage.Version);
        }

        [UIAuthorize]
        [HttpPost]
        [RequiresAccountConfirmation("delete a symbols package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> DeleteSymbolsPackage(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound,
                    string.Format(Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            if (ActionsRequiringPermissions.DeleteSymbolPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package)
                != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.SymbolsPackage_UploadNotAllowed);
            }

            if (package.PackageRegistration.IsLocked)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden,
                    string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id));
            }

            // Get all available symbol packages for a given package, ideally this should
            // always return one symbol package. For thoroughness we can cleanup the data
            // for any inconsistencies.
            var availableSymbolPackages = package
                .SymbolPackages
                .Where(sp => sp.StatusKey == PackageStatus.Available);

            if (availableSymbolPackages.Count() > 1)
            {
                _trace.Warning($"Multiple({availableSymbolPackages.Count()}) available symbol packages found for {package.Id}, {package.Version}");
            }

            if (availableSymbolPackages.Any())
            {
                foreach (var symbolPackage in availableSymbolPackages)
                {
                    await _symbolPackageUploadService.DeleteSymbolsPackageAsync(symbolPackage);
                }

                TempData["Message"] = Strings.SymbolsPackage_Deleted;

                await _auditingService.SaveAuditRecordAsync(
                    new PackageAuditRecord(package, AuditedPackageAction.SymbolsDelete, PackageDeletedVia.Web));

                _telemetryService.TrackSymbolPackageDeleteEvent(package.Id, package.Version);

                // Redirect to the package details page
                return Redirect(Url.Package(package, relativeUrl: true));
            }
            else
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest,
                    string.Format(Strings.SymbolsPackage_PackageNotAvailable, id, version));
            }
        }

        /// <summary>
        /// This is a redirect for a legacy route.
        /// The <see cref="Edit(string, string)"/> page was merged with <see cref="Delete(string, string)"/> and <see cref="ManagePackageOwners(string)"/> and is now a part of the <see cref="Manage(string, string)"/> page.
        /// </summary>
        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("edit a package")]
        public virtual ActionResult Edit(string id, string version)
        {
            return RedirectToActionPermanent(nameof(Manage));
        }

        [UIAuthorize]
        [HttpPost]
        [RequiresAccountConfirmation("unlist a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UpdateListed(string id, string version, bool? listed)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return HttpForbidden();
            }

            if (package.PackageRegistration.IsLocked)
            {
                return new HttpStatusCodeResult(403, string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id));
            }

            string action;
            if (!(listed ?? false))
            {
                action = "unlisted";
                await _packageUpdateService.MarkPackageUnlistedAsync(package);
            }
            else
            {
                action = "listed";
                await _packageUpdateService.MarkPackageListedAsync(package);
            }
            TempData["Message"] = string.Format(
                CultureInfo.CurrentCulture,
                "The package has been {0}. It may take several hours for this change to propagate through our system.",
                action);

            return Redirect(Url.ManagePackage(new TrivialPackageVersionModel(package)));
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("edit a package")]
        public virtual async Task<JsonResult> Edit(string id, string version, VerifyPackageRequest formData, string returnUrl)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return Json(HttpStatusCode.NotFound, new[] { new JsonValidationMessage(string.Format(Strings.PackageWithIdAndVersionNotFound, id, version)) });
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden, new[] { new JsonValidationMessage(Strings.Unauthorized) });
            }

            if (package.PackageRegistration.IsLocked)
            {
                return Json(HttpStatusCode.Forbidden, new[] { new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id)) });
            }

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values.SelectMany(v => v.Errors.Select(e => new JsonValidationMessage(e.ErrorMessage)));
                return Json(HttpStatusCode.BadRequest, errorMessages);
            }

            if (formData.Edit != null)
            {
                try
                {
                    // Update readme.md file, if modified.
                    var readmeChanged = await _readMeService.SaveReadMeMdIfChanged(
                        package,
                        formData.Edit,
                        Request.ContentEncoding,
                        commitChanges: true);

                    if (readmeChanged)
                    {
                        _telemetryService.TrackPackageReadMeChangeEvent(package, formData.Edit.ReadMe.SourceType, formData.Edit.ReadMeState);

                        // Add an auditing record for the package edit.
                        await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.Edit));
                    }
                }
                catch (ArgumentException ex) when (ex.Message.Contains(Strings.ReadMeUrlHostInvalid))
                {
                    // Thrown when ReadmeUrlHost is invalid.
                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.ReadMeUrlHostInvalid) });
                }
                catch (InvalidOperationException ex)
                {
                    // Thrown when readme max length exceeded, or unexpected file extension.
                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(ex.Message) });
                }
            }

            TempData["Message"] = "Your package's documentation has been updated.";

            return Json(new
            {
                location = returnUrl ?? Url.ManagePackage(new TrivialPackageVersionModel(id, version))
            });
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("accept ownership of a package")]
        public virtual Task<ActionResult> ConfirmPendingOwnershipRequestRedirect(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, redirect: true);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("accept ownership of a package")]
        public virtual Task<ActionResult> ConfirmPendingOwnershipRequest(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, redirect: false, accept: true);
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("reject ownership of a package")]
        public virtual Task<ActionResult> RejectPendingOwnershipRequestRedirect(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, redirect: true);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("reject ownership of a package")]
        public virtual Task<ActionResult> RejectPendingOwnershipRequest(string id, string username, string token)
        {
            return HandleOwnershipRequest(id, username, token, redirect: false, accept: false);
        }

        private async Task<ActionResult> HandleOwnershipRequest(
            string id,
            string username,
            string token,
            bool redirect,
            bool accept = false)
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
                // If the user is already an owner, clean up the invalid request.
                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(package, user);
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.AlreadyOwner));
            }

            var request = _packageOwnershipManagementService.GetPackageOwnershipRequest(package, user, token);
            if (request == null)
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Failure));
            }

            if (redirect)
            {
                return Redirect(Url.ManageMyReceivedPackageOwnershipRequests());
            }
            
            if (accept)
            {
                await _packageOwnershipManagementService.AddPackageOwnerAsync(package, user);

                await SendAddPackageOwnerNotificationAsync(package, user);

                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Success));
            }
            else
            {
                var requestingUser = request.RequestingOwner;

                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(package, user);

                var emailMessage = new PackageOwnershipRequestDeclinedMessage(_config, requestingUser, user, package);
                await _messageService.SendMessageAsync(emailMessage);

                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, user.Username, ConfirmOwnershipResult.Rejected));
            }
        }

        [HttpGet]
        [UIAuthorize]
        [RequiresAccountConfirmation("cancel pending ownership request")]
        public virtual ActionResult CancelPendingOwnershipRequest(string id, string requestingUsername, string pendingUsername)
        {
            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return View("ConfirmOwner", new PackageOwnerConfirmationModel(id, requestingUsername, ConfirmOwnershipResult.NotYourRequest));
            }

            var requestingUser = _userService.FindByUsername(requestingUsername);
            if (requestingUser == null)
            {
                return HttpNotFound();
            }

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

            return Redirect(Url.ManagePackageOwnership(id));
        }

        /// <summary>
        /// Send notification that a new package owner was added.
        /// </summary>
        /// <param name="package">Package to which owner was added.</param>
        /// <param name="newOwner">Owner added.</param>
        private Task SendAddPackageOwnerNotificationAsync(PackageRegistration package, User newOwner)
        {
            var packageUrl = Url.Package(package.Id, version: null, relativeUrl: false);
            Func<User, bool> notNewOwner = o => !o.Username.Equals(newOwner.Username, StringComparison.OrdinalIgnoreCase);

            // Notify existing owners
            var notNewOwners = package.Owners.Where(notNewOwner).ToList();
            var tasks = notNewOwners.Select(owner =>
            {
                var emailMessage = new PackageOwnerAddedMessage(_config, owner, newOwner, package, packageUrl);
                return _messageService.SendMessageAsync(emailMessage);
            });
            return Task.WhenAll(tasks);
        }

        [UIAuthorize]
        [HttpPost]
        [RequiresAccountConfirmation("upload a package")]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        public virtual async Task<JsonResult> VerifyPackage(VerifyPackageRequest formData)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errorMessages = ModelState.Values.SelectMany(v => v.Errors.Select(e => new JsonValidationMessage(e.ErrorMessage)));
                    return Json(HttpStatusCode.BadRequest, errorMessages);
                }

                var currentUser = GetCurrentUser();

                // Check that the owner specified in the form is valid
                var owner = _userService.FindByUsername(formData.Owner);

                if (owner == null)
                {
                    var message = new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.VerifyPackage_UserNonExistent, formData.Owner));
                    return Json(HttpStatusCode.BadRequest, new[] { message });
                }

                if (!owner.Confirmed)
                {
                    var message = new JsonValidationMessage(string.Format(CultureInfo.CurrentCulture, Strings.VerifyPackage_OwnerUnconfirmed, formData.Owner));
                    return Json(HttpStatusCode.BadRequest, new[] { message });
                }

                using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
                {
                    if (uploadFile == null)
                    {
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UploadNotFound) });
                    }

                    var packageArchiveReader = await SafeCreatePackage(currentUser, uploadFile);
                    if (packageArchiveReader == null)
                    {
                        // Send the user back
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
                    }

                    Debug.Assert(packageArchiveReader != null);

                    var packageMetadata = PackageMetadata.FromNuspecReader(
                        packageArchiveReader.GetNuspecReader(),
                        strict: true);

                    // Rule out problem scenario with multiple tabs - verification request (possibly with edits) was submitted by user
                    // viewing a different package to what was actually most recently uploaded
                    if (!(string.IsNullOrEmpty(formData.Id) || string.IsNullOrEmpty(formData.OriginalVersion)))
                    {
                        if (!(string.Equals(packageMetadata.Id, formData.Id, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(packageMetadata.Version.ToFullStringSafe(), formData.Version, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(packageMetadata.Version.OriginalVersion, formData.OriginalVersion, StringComparison.OrdinalIgnoreCase)))
                        {
                            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_PackageFileModified) });
                        }
                    }

                    // Dispose the uploadFile stream in the below callers, before deleting the uploaded symbols package file;
                    // otherwise the delete operation will fail.
                    // Note: Do not use the disposed stream after the calls below here(stating the obvious).
                    if (packageMetadata.IsSymbolsPackage())
                    {
                        return await VerifySymbolsPackageInternal(formData,
                            uploadFile,
                            packageArchiveReader,
                            packageMetadata,
                            currentUser,
                            owner);
                    }
                    else
                    {
                        return await VerifyPackageInternal(formData,
                            uploadFile,
                            packageArchiveReader,
                            packageMetadata,
                            currentUser,
                            owner);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryService.TrackPackagePushFailureEvent(id: null, version: null);
                throw ex;
            }
        }

        protected virtual async Task<JsonResult> VerifySymbolsPackageInternal(
            VerifyPackageRequest formData,
            Stream uploadFile,
            PackageArchiveReader packageArchiveReader,
            PackageMetadata packageMetadata,
            User currentUser,
            User owner)
        {
            string packageId = null;
            string packageVersion = null;
            try
            {
                // Perform initial validations again, the state could have been changed between the time 
                // when the symbols package file was uploaded and before submitting for publish.
                var symbolsPackageValidationResult = await _symbolPackageUploadService.ValidateUploadedSymbolsPackage(uploadFile, currentUser);
                var uploadResult = GetJsonResultOrNull(symbolsPackageValidationResult);
                if (uploadResult != null)
                {
                    return uploadResult;
                }

                var packageForUploadingSymbols = symbolsPackageValidationResult.Package;
                var existingPackageRegistration = packageForUploadingSymbols.PackageRegistration;
                packageId = existingPackageRegistration.Id;
                packageVersion = packageForUploadingSymbols.NormalizedVersion;

                if (existingPackageRegistration.IsLocked)
                {
                    return Json(HttpStatusCode.Forbidden, new[] {
                        new JsonValidationMessage(
                            string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, existingPackageRegistration.Id)) });
                }

                // Evaluate the permissions for the owner, the permissions for uploading a symbols should be same as that of
                // uploading a new version of a given package.
                var checkPermissionsOfUploadNewVersion = ActionsRequiringPermissions.UploadSymbolPackage.CheckPermissions(currentUser, owner, existingPackageRegistration);
                if (checkPermissionsOfUploadNewVersion != PermissionsCheckResult.Allowed)
                {
                    if (checkPermissionsOfUploadNewVersion == PermissionsCheckResult.AccountFailure)
                    {
                        // The user is not allowed to upload a new version on behalf of the owner specified in the form
                        var message = string.Format(CultureInfo.CurrentCulture,
                            Strings.UploadPackage_NewVersionOnBehalfOfUserNotAllowed,
                            currentUser.Username, owner.Username);
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
                    }

                    if (checkPermissionsOfUploadNewVersion == PermissionsCheckResult.PackageRegistrationFailure)
                    {
                        // The owner specified in the form is not allowed to upload a new version of the package
                        var message = string.Format(CultureInfo.CurrentCulture,
                            Strings.VerifyPackage_OwnerInvalid,
                            owner.Username, existingPackageRegistration.Id);
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
                    }

                    // An unknown error occurred.
                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
                }

                var commitResult = await _symbolPackageUploadService.CreateAndUploadSymbolsPackage(
                    packageForUploadingSymbols,
                    uploadFile.AsSeekableStream());

                switch (commitResult)
                {
                    case PackageCommitResult.Success:
                        break;
                    case PackageCommitResult.Conflict:
                        TempData["Message"] = Strings.SymbolsPackage_ConflictValidating;
                        return Json(HttpStatusCode.Conflict, new[] { new JsonValidationMessage(Strings.SymbolsPackage_ConflictValidating) });
                    default:
                        throw new NotImplementedException($"The symbols package commit result {commitResult} is not supported.");
                }

                await _auditingService.SaveAuditRecordAsync(
                    new PackageAuditRecord(packageForUploadingSymbols, AuditedPackageAction.SymbolsCreate, PackageCreatedVia.Web));

                _telemetryService.TrackSymbolPackagePushEvent(packageId, packageVersion);

                TempData["Message"] = string.Format(
                    CultureInfo.CurrentCulture, Strings.SymbolsPackage_UploadSuccessful, packageId, packageVersion);

                // Delete the uploaded file
                await DeleteUploadedFileForUser(currentUser, uploadFile);

                // Redirect to the package details page
                return Json(new
                {
                    location = Url.Package(packageId, packageVersion)
                });
            }
            catch (Exception ex)
            {
                ex.Log();
                _telemetryService.TrackSymbolPackagePushFailureEvent(packageId, packageVersion);
                return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
            }
        }

        protected virtual async Task<JsonResult> VerifyPackageInternal(
            VerifyPackageRequest formData,
            Stream uploadFile,
            PackageArchiveReader packageArchiveReader,
            PackageMetadata packageMetadata,
            User currentUser,
            User owner)
        {
            string packageId = null;
            NuGetVersion packageVersion = null;

            try
            {
                Package package;
                var packageStreamMetadata = new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(
                        uploadFile.AsSeekableStream(),
                        CoreConstants.Sha512HashAlgorithmId),
                    Size = uploadFile.Length,
                };

                packageId = packageMetadata.Id;
                packageVersion = packageMetadata.Version;

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
                            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
                        }
                        else if (checkPermissionsOfUploadNewId == PermissionsCheckResult.ReservedNamespaceFailure)
                        {
                            // The owner specified in the form is not allowed to push to a reserved namespace matching the new ID
                            var version = packageVersion.ToNormalizedString();
                            _telemetryService.TrackPackagePushNamespaceConflictEvent(packageId, version, currentUser, User.Identity);

                            var message = string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict);
                            return Json(HttpStatusCode.Conflict, new[] { new JsonValidationMessage(message) });
                        }

                        // An unknown error occurred.
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
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
                            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
                        }

                        if (checkPermissionsOfUploadNewVersion == PermissionsCheckResult.PackageRegistrationFailure)
                        {
                            // The owner specified in the form is not allowed to upload a new version of the package
                            var message = string.Format(CultureInfo.CurrentCulture,
                                Strings.VerifyPackage_OwnerInvalid,
                                owner.Username, existingPackageRegistration.Id);
                            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
                        }

                        // An unknown error occurred.
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
                    }
                }

                // Perform all the validations we can before adding the package to the entity context.
                var beforeValidationResult = await _packageUploadService.ValidateBeforeGeneratePackageAsync(packageArchiveReader, packageMetadata, currentUser);
                var beforeValidationJsonResult = GetJsonResultOrNull(beforeValidationResult);
                if (beforeValidationJsonResult != null)
                {
                    return beforeValidationJsonResult;
                }

                try
                {
                    package = await _packageUploadService.GeneratePackageAsync(
                        packageMetadata.Id,
                        packageArchiveReader,
                        packageStreamMetadata,
                        owner,
                        currentUser);

                    Debug.Assert(package.PackageRegistration != null);
                }
                catch (InvalidPackageException ex)
                {
                    _telemetryService.TraceException(ex);

                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(ex.Message) });
                }

                var packagePolicyResult = await _securityPolicyService.EvaluatePackagePoliciesAsync(
                    SecurityPolicyAction.PackagePush,
                    package,
                    currentUser,
                    owner,
                    HttpContext);

                if (!packagePolicyResult.Success)
                {
                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(packagePolicyResult.ErrorMessage) });
                }

                // Perform validations that require the package already being in the entity context.
                var afterValidationResult = await _packageUploadService.ValidateAfterGeneratePackageAsync(
                    package,
                    packageArchiveReader,
                    owner,
                    currentUser,
                    isNewPackageRegistration: existingPackageRegistration == null);

                var afterValidationJsonResult = GetJsonResultOrNull(afterValidationResult);
                if (afterValidationJsonResult != null)
                {
                    return afterValidationJsonResult;
                }

                if (formData.Edit != null)
                {
                    try
                    {
                        if (await _readMeService.SaveReadMeMdIfChanged(
                            package,
                            formData.Edit,
                            Request.ContentEncoding,
                            commitChanges: false))
                        {
                            _telemetryService.TrackPackageReadMeChangeEvent(package, formData.Edit.ReadMe.SourceType, formData.Edit.ReadMeState);
                        }
                    }
                    catch (ArgumentException ex) when (ex.Message.Contains(Strings.ReadMeUrlHostInvalid))
                    {
                        // Thrown when ReadmeUrlHost is invalid.
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.ReadMeUrlHostInvalid) });
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Thrown when readme max length exceeded, or unexpected file extension.
                        return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(ex.Message) });
                    }
                }

                await _packageService.PublishPackageAsync(package, commitChanges: false);

                if (!formData.Listed)
                {
                    await _packageUpdateService.MarkPackageUnlistedAsync(package, commitChanges: false, updateIndex: false);
                }

                // Commit the package to storage and to the database.
                uploadFile.Position = 0;
                try
                {
                    var commitResult = await _packageUploadService.CommitPackageAsync(
                        package,
                        uploadFile.AsSeekableStream());

                    switch (commitResult)
                    {
                        case PackageCommitResult.Success:
                            break;
                        case PackageCommitResult.Conflict:
                            TempData["Message"] = Strings.UploadPackage_IdVersionConflict;
                            return Json(HttpStatusCode.Conflict, new[] { new JsonValidationMessage(Strings.UploadPackage_IdVersionConflict) });
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
                        var message = new PackageAddedMessage(
                            _config,
                            package,
                            Url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                            Url.ReportPackage(package, relativeUrl: false),
                            Url.AccountSettings(relativeUrl: false),
                            warningMessages: null);

                        await _messageService.SendMessageAsync(message);
                    }

                    _telemetryService.TrackPackagePushEvent(package, currentUser, User.Identity);

                    TempData["Message"] = string.Format(
                        CultureInfo.CurrentCulture, Strings.SuccessfullyUploadedPackage, package.PackageRegistration.Id, package.Version);
                }
                catch (Exception e)
                {
                    e.Log();
                    return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(Strings.VerifyPackage_UnexpectedError) });
                }

                await DeleteUploadedFileForUser(currentUser, uploadFile);

                return Json(new
                {
                    location = Url.Package(package.PackageRegistration.Id, package.NormalizedVersion)
                });
            }
            catch (Exception)
            {
                _telemetryService.TrackPackagePushFailureEvent(packageId, packageVersion);
                throw;
            }
        }

        private async Task DeleteUploadedFileForUser(User currentUser, Stream uploadedFileStream)
        {
            try
            {
                // We should dispose the stream before we can delete the file from the disk.
                uploadedFileStream.Dispose();

                // delete the uploaded binary in the Uploads container
                await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);
            }
            catch (Exception e)
            {
                // Log the exception here and swallow it for now.
                // We want to know the delete has failed, but the user shouldn't get a failed request here since everything has actually gone through
                // Note that this will still lead to the strange behavior where the next time a user comes to the upload page, an upload will be "in progress"
                //  but verify will fail as the package has actually already been added, at which point, cancel will attempt the delete of this blob again.
                // An issue to clear in progress if it already exists has been logged at https://github.com/NuGet/NuGetGallery/issues/6192
                e.Log();
            }
        }

        private JsonResult GetJsonResultOrNull(PackageValidationResult validationResult)
        {
            var errorMessage = GetErrorMessageOrNull(validationResult);
            if (errorMessage == null)
            {
                return null;
            }

            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(errorMessage) });
        }

        private JsonResult GetJsonResultOrNull(SymbolPackageValidationResult validationResult)
        {
            HttpStatusCode httpStatusCode;
            switch (validationResult.Type)
            {
                case SymbolPackageValidationResultType.Accepted:
                    return null;
                case SymbolPackageValidationResultType.Invalid:
                case SymbolPackageValidationResultType.MissingPackage:
                    httpStatusCode = HttpStatusCode.BadRequest;
                    break;
                case SymbolPackageValidationResultType.SymbolsPackagePendingValidation:
                    httpStatusCode = HttpStatusCode.Conflict;
                    break;
                case SymbolPackageValidationResultType.UserNotAllowedToUpload:
                    httpStatusCode = HttpStatusCode.Forbidden;
                    break;
                default:
                    throw new NotImplementedException($"The symbol package validation result type {validationResult.Type} is not supported.");
            }

            return Json(httpStatusCode, new[] { new JsonValidationMessage(validationResult.Message) });
        }

        private static IValidationMessage GetErrorMessageOrNull(PackageValidationResult validationResult)
        {
            switch (validationResult.Type)
            {
                case PackageValidationResultType.Accepted:
                    return null;
                case PackageValidationResultType.Invalid:
                    return validationResult.Message;
                default:
                    throw new NotImplementedException($"The package validation result type {validationResult.Type} is not supported.");
            }
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

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> CancelUpload()
        {
            var currentUser = GetCurrentUser();
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            return Json(null);
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> PreviewReadMe(ReadMeRequest formData)
        {
            if (formData == null || !_readMeService.HasReadMeSource(formData))
            {
                return Json(HttpStatusCode.BadRequest, new[] { Strings.PreviewReadMe_ReadMeMissing });
            }

            try
            {
                var readMeResult = await _readMeService.GetReadMeHtmlAsync(formData, Request.ContentEncoding);
                return Json(readMeResult);
            }
            catch (Exception ex)
            {
                return Json(HttpStatusCode.BadRequest, new[] {
                    string.Format(CultureInfo.CurrentCulture, Strings.PreviewReadMe_ConversionFailed, ex.Message) });
            }
        }

        [UIAuthorize]
        [HttpGet]
        public virtual async Task<JsonResult> GetReadMeMd(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return Json(HttpStatusCode.NotFound, null, JsonRequestBehavior.AllowGet);
            }

            if (ActionsRequiringPermissions.EditPackage.CheckPermissionsOnBehalfOfAnyAccount(GetCurrentUser(), package) != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden, null, JsonRequestBehavior.AllowGet);
            }
            
            var request = new EditPackageVersionReadMeRequest();
            if (package.HasReadMe)
            {
                var readMe = await _readMeService.GetReadMeMdAsync(package);
                if (package.HasReadMe)
                {
                    request.ReadMe.SourceType = ReadMeService.TypeWritten;
                    request.ReadMe.SourceText = readMe;
                }
            }

            return Json(request, JsonRequestBehavior.AllowGet);
        }

        [UIAuthorize]
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
                return HttpForbidden();
            }

            await _packageService.SetLicenseReportVisibilityAsync(package, visible);

            TempData["Message"] = string.Format(
                CultureInfo.CurrentCulture,
                "The license report for this package has been {0}. It may take several hours for this change to propagate through our system.",
                visible ? "enabled" : "disabled");

            // Update the index
            _indexingService.UpdatePackage(package);

            return Redirect(urlFactory(package, /*relativeUrl:*/ true));
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> SetRequiredSigner(string id, string username)
        {
            var packageRegistration = _packageService.FindPackageRegistrationById(id);

            if (packageRegistration == null)
            {
                return Json(HttpStatusCode.NotFound);
            }

            var currentUser = GetCurrentUser();

            var wasAADLoginOrMultiFactorAuthenticated = User.WasMultiFactorAuthenticated() || User.WasAzureActiveDirectoryAccountUsedForSignin();
            var canManagePackageRequiredSigner = wasAADLoginOrMultiFactorAuthenticated
                && ActionsRequiringPermissions
                    .ManagePackageRequiredSigner
                    .CheckPermissionsOnBehalfOfAnyAccount(currentUser, packageRegistration) == PermissionsCheckResult.Allowed;
            if (!canManagePackageRequiredSigner)
            {
                return Json(HttpStatusCode.Forbidden);
            }

            User signer = null;

            if (!string.IsNullOrEmpty(username))
            {
                signer = _userService.FindByUsername(username);

                if (signer == null)
                {
                    return Json(HttpStatusCode.NotFound);
                }
            }

            await _packageService.SetRequiredSignerAsync(packageRegistration, signer);

            return Json(HttpStatusCode.OK);
        }

        private JsonResult FailedToReadFile(Exception ex)
        {
            ex.Log();

            var message = Strings.FailedToReadUploadFile;
            if (ex is InvalidPackageException || ex is InvalidDataException || ex is EntityException)
            {
                message = ex.Message;
            }

            return Json(HttpStatusCode.BadRequest, new[] { new JsonValidationMessage(message) });
        }

        // this method exists to make unit testing easier
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
                case GalleryConstants.AlphabeticSortOrder:
                    return "PackageRegistration.Id";
                case GalleryConstants.RecentSortOrder:
                    return "Published desc";

                default:
                    return "PackageRegistration.DownloadCount desc";
            }
        }

        // Determine whether an 'Edit' string submitted differs from one read from the package.
        private static bool IsDifferent(string posted, string package)
        {
            if (string.IsNullOrEmpty(posted) || string.IsNullOrEmpty(package))
            {
                return string.IsNullOrEmpty(posted) != string.IsNullOrEmpty(package);
            }

            // Compare non-empty strings
            // Ignore those pesky '\r' characters which screw up comparisons.
            return !string.Equals(posted.Replace("\r", ""), package.Replace("\r", ""), StringComparison.Ordinal);
        }

        private static HttpStatusCodeResult HttpForbidden()
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
        }
    }
}
