// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class PackageStagingUploadService : IPackageStagingUploadService
    {
        private readonly IApiScopeEvaluator _apiScopeEvaluator;

        private readonly IFeatureFlagService _featureFlagService;

        private readonly IPackageService _packageService;

        private readonly IPackageUploadService _packageUploadService;

        private readonly IReservedNamespaceService _reservedNamespaceService;

        private readonly ISecurityPolicyService _securityPolicyService;

        private readonly IStagingBlobService _stagingBlobService;

        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;

        private readonly IValidationMessageEmitter<Package> _validationMessageEmitter;

        public PackageStagingUploadService(
            IApiScopeEvaluator apiScopeEvaluator,
            IFeatureFlagService featureFlagService,
            IPackageService packageService,
            IPackageUploadService packageUploadService,
            IReservedNamespaceService reservedNamespaceService,
            ISecurityPolicyService securityPolicyService,
            IStagingBlobService stagingBlobService,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IValidationMessageEmitter<Package> validationMessageEmitter)
        {
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageUploadService = packageUploadService ?? throw new ArgumentNullException(nameof(packageUploadService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _validationMessageEmitter = validationMessageEmitter ?? throw new ArgumentNullException(nameof(validationMessageEmitter));
        }

        public async Task<PackageStagingResult> StagePackageAsync(User currentUser, IEnumerable<Scope> scopes, HttpContextBase httpContext, Stream packageFile)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var userPolicyResult = await _securityPolicyService.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, httpContext);
            if (!userPolicyResult.Success)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, userPolicyResult.ErrorMessage);
            }

            try
            {
                using var seekableStream = packageFile.AsSeekableStream();

                var validationError = ZipArchiveHelpers.GetArchiveValidationError(seekableStream);
                if (validationError != null)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, validationError);
                }

                using var packageReader = await ValidatePackageAsync(seekableStream);

                validationError = ValidateManifest(packageReader, out var packageMetadata);
                if (validationError != null)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, validationError);
                }

                var id = packageMetadata.Id;
                var version = packageMetadata.Version;
                var packageRegistration = _packageService.FindPackageRegistrationById(id);
                var authorizationError = AuthorizeStaging(currentUser, scopes, packageRegistration, id, version, out var owner);
                if (authorizationError != null)
                {
                    return authorizationError;
                }

                var beforeValidation = await _packageUploadService.ValidateBeforeGeneratePackageAsync(packageReader, packageMetadata, currentUser);
                if (beforeValidation.Type != PackageValidationResultType.Accepted)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, beforeValidation.Message.PlainTextMessage);
                }

                var streamMetadata = new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(seekableStream, CoreConstants.Sha512HashAlgorithmId),
                    Size = seekableStream.Length,
                };

                seekableStream.Position = 0;
                var package = await _packageUploadService.GeneratePackageAsync(id, packageReader, streamMetadata, owner, currentUser);
                var packagePolicyResult = await _securityPolicyService.EvaluatePackagePoliciesAsync(SecurityPolicyAction.PackagePush, package, currentUser, owner, httpContext);
                if (!packagePolicyResult.Success)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, packagePolicyResult.ErrorMessage);
                }

                var afterValidation = await _packageUploadService.ValidateAfterGeneratePackageAsync(package, packageReader, owner, currentUser, isNewPackageRegistration: packageRegistration == null);
                if (afterValidation.Type != PackageValidationResultType.Accepted)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, afterValidation.Message.PlainTextMessage);
                }

                seekableStream.Position = 0;
                var commitResult = await CommitPackageAsync(package, owner, seekableStream);
                if (commitResult == PackageCommitResult.Conflict)
                {
                    return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.UploadPackage_IdVersionConflict);
                }

                var warnings = CreateWarnings(beforeValidation, afterValidation, packagePolicyResult);
                return PackageStagingResult.Created(warnings);
            }
            catch (Exception ex) when (ex is InvalidPackageException || ex is InvalidDataException || ex is PackagingException || ex is EntityException)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        private async Task<PackageArchiveReader> ValidatePackageAsync(Stream packageFile)
        {
            PackageArchiveReader packageReader = null;
            try
            {
                packageReader = new PackageArchiveReader(packageFile, leaveStreamOpen: true);
                await _packageService.EnsureValid(packageReader);
                return packageReader;
            }
            catch (Exception exception)
            {
                exception.Log();
                packageReader?.Dispose();

                if (exception is InvalidPackageException || exception is InvalidDataException || exception is EntityException)
                {
                    throw;
                }

                throw new InvalidPackageException(Strings.FailedToReadUploadFile, exception);
            }
        }

        private string ValidateManifest(PackageArchiveReader packageReader, out PackageMetadata packageMetadata)
        {
            var errors = ManifestValidator.Validate(
                packageReader.GetNuspec(),
                id =>
                {
                    return _featureFlagService.IsInvalidPackageIdAllowedForAllPackages()
                        || (_featureFlagService.IsInvalidPackageIdAllowedForExistingPackages() && _packageService.FindPackageRegistrationById(id) != null);
                },
                out var nuspec,
                out packageMetadata).ToArray();
            if (errors.Length > 0)
            {
                var errorsString = string.Join("', '", errors.Select(error => error.ErrorMessage));
                return string.Format(
                    CultureInfo.CurrentCulture,
                    errors.Length > 1 ? Strings.UploadPackage_InvalidNuspecMultiple : Strings.UploadPackage_InvalidNuspec,
                    errorsString);
            }

            if (nuspec.GetMinClientVersion() > GalleryConstants.MaxSupportedMinClientVersion)
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_MinClientVersionOutOfRange, nuspec.GetMinClientVersion());
            }

            return null;
        }

        private PackageStagingResult AuthorizeStaging(User currentUser, IEnumerable<Scope> scopes, PackageRegistration packageRegistration, string id, NuGetVersion version, out User owner)
        {
            var authorizationResult = EvaluateAuthorization(currentUser, scopes, packageRegistration, id);
            owner = authorizationResult.Owner;
            if (!authorizationResult.IsSuccessful())
            {
                return GetAuthorizationFailure(authorizationResult);
            }

            if (!_featureFlagService.IsPackageStagingEnabled(owner))
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "Package staging is not enabled.");
            }

            if (packageRegistration?.IsLocked == true)
            {
                return PackageStagingResult.Error(HttpStatusCode.Forbidden, "The package ID is locked and cannot be staged.");
            }

            if (_packageService.GetPackageStatus(id, version) != null)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, string.Format(Strings.PackageExistsAndCannotBeModified, id, version.ToNormalizedString()));
            }

            return null;
        }

        private ApiScopeEvaluationResult EvaluateAuthorization(User currentUser, IEnumerable<Scope> scopes, PackageRegistration packageRegistration, string id)
        {
            if (packageRegistration == null)
            {
                return _apiScopeEvaluator.Evaluate(
                    currentUser,
                    scopes,
                    ActionsRequiringPermissions.UploadNewPackageId,
                    new ActionOnNewPackageContext(id, _reservedNamespaceService),
                    NuGetScopes.PackagePush);
            }

            return _apiScopeEvaluator.Evaluate(
                currentUser,
                scopes,
                ActionsRequiringPermissions.UploadNewPackageVersion,
                packageRegistration,
                NuGetScopes.PackagePushVersion,
                NuGetScopes.PackagePush);
        }

        private static PackageStagingResult GetAuthorizationFailure(ApiScopeEvaluationResult result)
        {
            if (result.IsSuccessful())
            {
                throw new ArgumentException($"{nameof(result)} is not a failed evaluation.", nameof(result));
            }

            if (result.PermissionsCheckResult == PermissionsCheckResult.ReservedNamespaceFailure)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);
            }

            if (result.PermissionsCheckResult == PermissionsCheckResult.OwnerlessReservedNamespaceFailure)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.UploadPackage_OwnerlessIdNamespaceConflict);
            }

            if (result.PermissionsCheckResult == PermissionsCheckResult.Allowed && !result.IsOwnerConfirmed)
            {
                return PackageStagingResult.Error(HttpStatusCode.Forbidden, Strings.ApiKeyOwnerUnconfirmed);
            }

            if (result.PermissionsCheckResult == PermissionsCheckResult.Allowed && result.IsOwnerLocked)
            {
                return PackageStagingResult.Error(HttpStatusCode.Forbidden, Strings.ApiKeyOwnerLocked);
            }

            return PackageStagingResult.Error(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
        }

        private static IReadOnlyList<IValidationMessage> CreateWarnings(PackageValidationResult beforeValidation, PackageValidationResult afterValidation, SecurityPolicyResult packagePolicyResult)
        {
            var warnings = new List<IValidationMessage>();
            warnings.AddRange(beforeValidation.Warnings);
            warnings.AddRange(afterValidation.Warnings);
            warnings.AddRange(packagePolicyResult.WarningMessages.Select(warning => new PlainTextOnlyValidationMessage(warning)));
            return warnings;
        }

        private async Task<PackageCommitResult> CommitPackageAsync(Package package, User owner, Stream packageFile)
        {
            var file = await _stagingBlobService.SavePackageFileAsync(package.PackageRegistration.Id, package.NormalizedVersion, packageFile);
            var validationTrackingId = Guid.NewGuid();

            await _packageService.UpdatePackageStatusAsync(package, PackageStatus.Staged, commitChanges: false);
            var stagedPackage = new StagedPackage
            {
                Package = package,
                OwnerKey = owner.Key,
                BlobPath = file.Path,
                BlobETag = file.ETag,
                Status = StagedPackageStatus.Validating,
                ValidationTrackingId = validationTrackingId,
                UploadedDate = DateTime.UtcNow,
            };
            _stagedPackageRepository.InsertOnCommit(stagedPackage);

            try
            {
                await _stagedPackageRepository.CommitChangesAsync();
                var validationStarted = await _validationMessageEmitter.StartValidationAsync(package, validationTrackingId);
                if (!validationStarted)
                {
                    // Without asynchronous validation, the staged package is immediately ready.
                    stagedPackage.Status = StagedPackageStatus.Ready;
                    await _stagedPackageRepository.CommitChangesAsync();
                }
            }
            catch (Exception exception) when (IsConflict(exception))
            {
                return PackageCommitResult.Conflict;
            }

            return PackageCommitResult.Success;
        }

        private static bool IsConflict(Exception exception)
        {
            if (exception is DbUpdateConcurrencyException)
            {
                return true;
            }

            return exception is DbUpdateException updateException && updateException.IsSqlUniqueConstraintViolation();
        }

    }
}
