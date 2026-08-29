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
        private readonly IEntitiesContext _entitiesContext;

        private readonly IApiScopeEvaluator _apiScopeEvaluator;

        private readonly IFeatureFlagService _featureFlagService;

        private readonly IPackageService _packageService;

        private readonly IPackageUploadService _packageUploadService;

        private readonly IReservedNamespaceService _reservedNamespaceService;

        private readonly ISecurityPolicyService _securityPolicyService;

        private readonly IStagingBlobService _stagingBlobService;

        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;

        private readonly IStagedPackageValidationMessageEmitter _stagedValidationMessageEmitter;

        public PackageStagingUploadService(
            IEntitiesContext entitiesContext,
            IApiScopeEvaluator apiScopeEvaluator,
            IFeatureFlagService featureFlagService,
            IPackageService packageService,
            IPackageUploadService packageUploadService,
            IReservedNamespaceService reservedNamespaceService,
            ISecurityPolicyService securityPolicyService,
            IStagingBlobService stagingBlobService,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IStagedPackageValidationMessageEmitter stagedValidationMessageEmitter)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageUploadService = packageUploadService ?? throw new ArgumentNullException(nameof(packageUploadService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagedValidationMessageEmitter = stagedValidationMessageEmitter ?? throw new ArgumentNullException(nameof(stagedValidationMessageEmitter));
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

                var streamMetadata = new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(seekableStream, CoreConstants.Sha512HashAlgorithmId),
                    Size = seekableStream.Length,
                };

                var existingPackageResult = GetExistingPackageResult(id, version, owner, streamMetadata.Hash, out var existingPackage, out var currentAttempt);
                if (existingPackageResult != null)
                {
                    return existingPackageResult;
                }

                var beforeValidation = await _packageUploadService.ValidateBeforeGeneratePackageAsync(packageReader, packageMetadata, currentUser);
                if (beforeValidation.Type != PackageValidationResultType.Accepted)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, beforeValidation.Message.PlainTextMessage);
                }

                Package candidatePackage;
                if (existingPackage == null)
                {
                    seekableStream.Position = 0;
                    candidatePackage = await _packageUploadService.GeneratePackageAsync(id, packageReader, streamMetadata, owner, currentUser);
                }
                else
                {
                    candidatePackage = new Package { PackageRegistration = packageRegistration };
                    _packageService.EnrichPackageFromNuGetPackage(candidatePackage, packageReader, packageMetadata, streamMetadata, currentUser);
                }

                var packagePolicyResult = await _securityPolicyService.EvaluatePackagePoliciesAsync(SecurityPolicyAction.PackagePush, candidatePackage, currentUser, owner, httpContext);
                if (!packagePolicyResult.Success)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, packagePolicyResult.ErrorMessage);
                }

                var afterValidation = await _packageUploadService.ValidateAfterGeneratePackageAsync(candidatePackage, packageReader, owner, currentUser, isNewPackageRegistration: packageRegistration == null);
                if (afterValidation.Type != PackageValidationResultType.Accepted)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, afterValidation.Message.PlainTextMessage);
                }

                var package = candidatePackage;
                if (existingPackage != null)
                {
                    UpdateExistingPackage(existingPackage, candidatePackage, packageReader, packageMetadata, streamMetadata, currentUser, currentAttempt.Status == StagedPackageStatus.Deleted);
                    package = existingPackage;
                }

                seekableStream.Position = 0;
                var commitResult = await CommitPackageAsync(package, owner, seekableStream, streamMetadata.Hash, currentAttempt);
                if (commitResult == PackageCommitResult.Conflict)
                {
                    return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.UploadPackage_IdVersionConflict);
                }

                var warnings = CreateWarnings(beforeValidation, afterValidation, packagePolicyResult);
                if (existingPackage == null)
                {
                    return PackageStagingResult.Created(warnings);
                }

                return PackageStagingResult.Ok(warnings);
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

            return null;
        }

        private PackageStagingResult GetExistingPackageResult(
            string id,
            NuGetVersion version,
            User owner,
            string uploadHash,
            out Package existingPackage,
            out StagedPackage currentAttempt)
        {
            existingPackage = null;
            currentAttempt = null;

            var packageStatus = _packageService.GetPackageStatus(id, version);
            if (packageStatus == null)
            {
                return null;
            }

            if (packageStatus != PackageStatus.Staged && packageStatus != PackageStatus.Deleted)
            {
                return CreateExistingPackageConflict(id, version);
            }

            existingPackage = _packageService.FindPackageByIdAndVersionStrict(id, version.ToNormalizedString());
            if (existingPackage == null)
            {
                return CreateExistingPackageConflict(id, version);
            }

            currentAttempt = GetCurrentAttempt(existingPackage.Key);

            var isSameOwner = currentAttempt?.OwnerKey == owner.Key;
            var isActive = currentAttempt?.Status == StagedPackageStatus.Validating || currentAttempt?.Status == StagedPackageStatus.Ready;
            var isIdentical = string.Equals(currentAttempt?.UploadHash, uploadHash, StringComparison.Ordinal);

            // A superseded attempt cannot be current because its successor must have a higher key.
            if (!isSameOwner || currentAttempt.Status == StagedPackageStatus.Superseded)
            {
                return CreateExistingPackageConflict(id, version);
            }

            if (isActive && isIdentical)
            {
                return PackageStagingResult.Ok();
            }

            // A deleted Package can be restaged only when its latest staging attempt is also Deleted.
            // This proves the version was deleted from staging before promotion. Packages deleted after
            // normal push or promotion have no current staging attempt and remain conflicts.
            var canCreateSuccessor = packageStatus == PackageStatus.Staged
                || (packageStatus == PackageStatus.Deleted && currentAttempt.Status == StagedPackageStatus.Deleted);
            if (!canCreateSuccessor)
            {
                return CreateExistingPackageConflict(id, version);
            }

            return null;
        }

        private StagedPackage GetCurrentAttempt(int packageKey)
        {
            return _stagedPackageRepository
                .GetAll()
                .Where(candidate => candidate.PackageKey == packageKey)
                .OrderByDescending(candidate => candidate.Key)
                .FirstOrDefault();
        }

        private static PackageStagingResult CreateExistingPackageConflict(string id, NuGetVersion version)
        {
            return PackageStagingResult.Error(HttpStatusCode.Conflict, string.Format(Strings.PackageExistsAndCannotBeModified, id, version.ToNormalizedString()));
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

        private void UpdateExistingPackage(
            Package package,
            Package candidatePackage,
            PackageArchiveReader packageReader,
            PackageMetadata packageMetadata,
            PackageStreamMetadata streamMetadata,
            User currentUser,
            bool wasDeleted)
        {
            var listed = package.Listed;
            ClearPackageMetadata(package);
            _packageService.EnrichPackageFromNuGetPackage(package, packageReader, packageMetadata, streamMetadata, currentUser);
            package.PackageRegistration = candidatePackage.PackageRegistration;
            if (!wasDeleted)
            {
                package.Listed = listed;
            }
        }

        private void ClearPackageMetadata(Package package)
        {
#pragma warning disable 618
            RemoveAll(package.Authors);
#pragma warning restore 618
            RemoveAll(package.Dependencies);
            RemoveAll(package.PackageTypes);
            RemoveAll(package.SupportedFrameworks);
        }

        private void RemoveAll<TEntity>(ICollection<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                _entitiesContext.Set<TEntity>().Remove(entity);
            }

            entities.Clear();
        }

        private async Task<PackageCommitResult> CommitPackageAsync(
            Package package,
            User owner,
            Stream packageFile,
            string uploadHash,
            StagedPackage previousAttempt)
        {
            var file = await _stagingBlobService.SavePackageFileAsync(package.PackageRegistration.Id, package.NormalizedVersion, packageFile);

            await _packageService.UpdatePackageStatusAsync(package, PackageStatus.Staged, commitChanges: false);
            var stagedPackage = new StagedPackage
            {
                Package = package,
                OwnerKey = owner.Key,
                UploadedBlobPath = file.Path,
                UploadedBlobETag = file.ETag,
                UploadHash = uploadHash,
                Status = StagedPackageStatus.Validating,
                UploadedDate = DateTime.UtcNow,
            };
            _stagedPackageRepository.InsertOnCommit(stagedPackage);

            if (previousAttempt?.Status == StagedPackageStatus.Validating || previousAttempt?.Status == StagedPackageStatus.Ready)
            {
                previousAttempt.Status = StagedPackageStatus.Superseded;
            }

            try
            {
                stagedPackage.Status = await _stagedValidationMessageEmitter.StartValidationAsync(stagedPackage);

                await _stagedPackageRepository.CommitChangesAsync();
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
