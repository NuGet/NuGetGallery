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

        private readonly IStagedPackageValidationMessageEmitter _stagedValidationMessageEmitter;

        public PackageStagingUploadService(
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

        public async Task<PackageStagingResult> StagePackageAsync(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            HttpContextBase httpContext,
            Stream packageFile)
        {
            ValidateRequest(currentUser, httpContext, packageFile);

            var requestError = await ValidateUserPolicyAsync(currentUser, httpContext);
            if (requestError != null)
            {
                return requestError;
            }

            try
            {
                using var upload = await PrepareUploadAsync(packageFile);
                var targetError = ResolveApiUploadTarget(currentUser, scopes, upload, out var target);
                if (targetError != null)
                {
                    return targetError;
                }

                return await ProcessUploadAsync(currentUser, httpContext, upload, target);
            }
            catch (Exception exception) when (IsInvalidPackage(exception))
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, exception.Message);
            }
        }

        public async Task<PackageStagingResult> ReplacePackageAsync(
            User currentUser,
            HttpContextBase httpContext,
            StagedPackage stagedPackage,
            Stream packageFile)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            ValidateRequest(currentUser, httpContext, packageFile);

            try
            {
                using var upload = await PrepareUploadAsync(packageFile);
                var targetError = ResolveUiReplacementTarget(stagedPackage, upload, out var target);
                if (targetError != null)
                {
                    return targetError;
                }

                return await ProcessUploadAsync(currentUser, httpContext, upload, target);
            }
            catch (Exception exception) when (IsInvalidPackage(exception))
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, exception.Message);
            }
        }

        private static void ValidateRequest(User currentUser, HttpContextBase httpContext, Stream packageFile)
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
        }

        private async Task<PackageStagingResult> ValidateUserPolicyAsync(User currentUser, HttpContextBase httpContext)
        {
            var userPolicyResult = await _securityPolicyService.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, httpContext);
            if (!userPolicyResult.Success)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, userPolicyResult.ErrorMessage);
            }

            return null;
        }

        private async Task<PreparedPackageUpload> PrepareUploadAsync(Stream packageFile)
        {
            var seekableStream = packageFile.AsSeekableStream();
            try
            {
                var validationError = ZipArchiveHelpers.GetArchiveValidationError(seekableStream);
                if (validationError != null)
                {
                    throw new InvalidPackageException(validationError);
                }

                var packageReader = await ValidatePackageAsync(seekableStream);
                try
                {
                    validationError = ValidateManifest(packageReader, out var packageMetadata);
                    if (validationError != null)
                    {
                        throw new InvalidPackageException(validationError);
                    }

                    return new PreparedPackageUpload(seekableStream, packageReader, packageMetadata);
                }
                catch
                {
                    packageReader.Dispose();
                    throw;
                }
            }
            catch
            {
                seekableStream.Dispose();
                throw;
            }
        }

        private PackageStagingResult ResolveApiUploadTarget(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            PreparedPackageUpload upload,
            out StagingTarget target)
        {
            var packageRegistration = _packageService.FindPackageRegistrationById(upload.Id);
            var authorizationResult = EvaluateAuthorization(currentUser, scopes, packageRegistration, upload.Id);
            if (!authorizationResult.IsSuccessful())
            {
                target = null;
                return GetAuthorizationFailure(authorizationResult);
            }

            Package existingPackage = null;
            var packageStatus = _packageService.GetPackageStatus(upload.Id, upload.PackageMetadata.Version);
            if (packageStatus != null)
            {
                existingPackage = _packageService.FindPackageByIdAndVersionStrict(upload.Id, upload.NormalizedVersion);
            }

            var currentAttempt = GetCurrentAttempt(existingPackage);

            return ResolveTarget(
                upload,
                packageRegistration,
                packageStatus,
                existingPackage,
                currentAttempt,
                authorizationResult.Owner,
                allowCreate: true,
                out target);
        }

        private PackageStagingResult ResolveUiReplacementTarget(
            StagedPackage authorizedStagedPackage,
            PreparedPackageUpload upload,
            out StagingTarget target)
        {
            var hasMatchingId = string.Equals(authorizedStagedPackage.Package.PackageRegistration.Id, upload.Id, StringComparison.OrdinalIgnoreCase);
            var hasMatchingVersion = string.Equals(authorizedStagedPackage.Package.NormalizedVersion, upload.NormalizedVersion, StringComparison.OrdinalIgnoreCase);
            if (!hasMatchingId || !hasMatchingVersion)
            {
                target = null;
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, "The replacement package identity does not match the staged package.");
            }

            var packageRegistration = _packageService.FindPackageRegistrationById(upload.Id);

            Package existingPackage = null;
            var packageStatus = _packageService.GetPackageStatus(upload.Id, upload.PackageMetadata.Version);
            if (packageStatus != null)
            {
                existingPackage = _packageService.FindPackageByIdAndVersionStrict(upload.Id, upload.NormalizedVersion);
            }

            var currentAttempt = GetCurrentAttempt(existingPackage);
            if (currentAttempt?.Key != authorizedStagedPackage.Key)
            {
                target = null;
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The staged package was not found.");
            }

            return ResolveTarget(
                upload,
                packageRegistration,
                packageStatus,
                existingPackage,
                currentAttempt,
                authorizedStagedPackage.Owner,
                allowCreate: false,
                out target);
        }

        private PackageStagingResult ResolveTarget(
            PreparedPackageUpload upload,
            PackageRegistration packageRegistration,
            PackageStatus? packageStatus,
            Package existingPackage,
            StagedPackage currentAttempt,
            User owner,
            bool allowCreate,
            out StagingTarget target)
        {
            target = null;
            if (!_featureFlagService.IsPackageStagingEnabled(owner))
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "Package staging is not enabled.");
            }

            if (packageRegistration?.IsLocked == true)
            {
                return PackageStagingResult.Error(HttpStatusCode.Forbidden, "The package ID is locked and cannot be staged.");
            }

            if (packageStatus == null)
            {
                if (!allowCreate)
                {
                    return PackageStagingResult.Error(HttpStatusCode.NotFound, "The staged package was not found.");
                }

                target = new StagingTarget(packageRegistration, existingPackage: null, currentAttempt: null, owner);
                return null;
            }

            if (existingPackage == null)
            {
                if (allowCreate)
                {
                    return CreateExistingPackageConflict(upload.Id, upload.PackageMetadata.Version);
                }

                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The staged package was not found.");
            }

            if (currentAttempt == null)
            {
                return CreateExistingPackageConflict(upload.Id, upload.PackageMetadata.Version);
            }

            if (currentAttempt.OwnerKey != owner.Key)
            {
                return CreateExistingPackageConflict(upload.Id, upload.PackageMetadata.Version);
            }

            if (currentAttempt.Status == StagedPackageStatus.Superseded)
            {
                return CreateExistingPackageConflict(upload.Id, upload.PackageMetadata.Version);
            }

            var canReplace = packageStatus == PackageStatus.Staged;

            // A deleted Package can be restaged only when its latest staging attempt is also Deleted.
            // This proves the version was deleted from staging before promotion. Packages deleted after
            // normal push or promotion have no current staging attempt and remain conflicts.
            var canReactivate = packageStatus == PackageStatus.Deleted
                && currentAttempt.Status == StagedPackageStatus.Deleted;
            if (!canReplace && !canReactivate)
            {
                return CreateExistingPackageConflict(upload.Id, upload.PackageMetadata.Version);
            }

            target = new StagingTarget(packageRegistration, existingPackage, currentAttempt, owner);
            return null;
        }

        private async Task<PackageStagingResult> ProcessUploadAsync(
            User currentUser,
            HttpContextBase httpContext,
            PreparedPackageUpload upload,
            StagingTarget target)
        {
            var streamMetadata = new PackageStreamMetadata
            {
                HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                Hash = CryptographyService.GenerateHash(upload.PackageFile, CoreConstants.Sha512HashAlgorithmId),
                Size = upload.PackageFile.Length,
            };

            var isValidating = target.CurrentAttempt?.Status == StagedPackageStatus.Validating;
            var isReady = target.CurrentAttempt?.Status == StagedPackageStatus.Ready;
            var isIdentical = string.Equals(target.CurrentAttempt?.UploadHash, streamMetadata.Hash, StringComparison.Ordinal);
            if ((isValidating || isReady) && isIdentical)
            {
                return PackageStagingResult.Ok();
            }

            var beforeValidation = await _packageUploadService.ValidateBeforeGeneratePackageAsync(
                upload.PackageReader,
                upload.PackageMetadata,
                currentUser);
            if (beforeValidation.Type != PackageValidationResultType.Accepted)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, beforeValidation.Message.PlainTextMessage);
            }

            Package candidatePackage;
            if (target.ExistingPackage == null)
            {
                upload.PackageFile.Position = 0;
                candidatePackage = await _packageUploadService.GeneratePackageAsync(
                    upload.Id,
                    upload.PackageReader,
                    streamMetadata,
                    target.Owner,
                    currentUser);
            }
            else
            {
                candidatePackage = new Package { PackageRegistration = target.PackageRegistration };
                _packageService.EnrichPackageFromNuGetPackage(
                    candidatePackage,
                    upload.PackageReader,
                    upload.PackageMetadata,
                    streamMetadata,
                    currentUser);
            }

            var packagePolicyResult = await _securityPolicyService.EvaluatePackagePoliciesAsync(
                SecurityPolicyAction.PackagePush,
                candidatePackage,
                currentUser,
                target.Owner,
                httpContext);
            if (!packagePolicyResult.Success)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, packagePolicyResult.ErrorMessage);
            }

            var afterValidation = await _packageUploadService.ValidateAfterGeneratePackageAsync(
                candidatePackage,
                upload.PackageReader,
                target.Owner,
                currentUser,
                isNewPackageRegistration: target.PackageRegistration == null);
            if (afterValidation.Type != PackageValidationResultType.Accepted)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, afterValidation.Message.PlainTextMessage);
            }

            var package = candidatePackage;
            if (target.ExistingPackage != null)
            {
                UpdateExistingPackage(
                    target.CurrentAttempt,
                    candidatePackage,
                    upload.PackageReader,
                    upload.PackageMetadata,
                    streamMetadata,
                    currentUser);
                package = target.ExistingPackage;
            }

            upload.PackageFile.Position = 0;
            var commitResult = await CommitPackageAsync(
                package,
                target.Owner,
                upload.PackageFile,
                streamMetadata.Hash,
                target.CurrentAttempt);
            if (commitResult == PackageCommitResult.Conflict)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.UploadPackage_IdVersionConflict);
            }

            var warnings = CreateWarnings(beforeValidation, afterValidation, packagePolicyResult);
            if (target.ExistingPackage == null)
            {
                return PackageStagingResult.Created(warnings);
            }

            return PackageStagingResult.Ok(warnings);
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

        private StagedPackage GetCurrentAttempt(int packageKey)
        {
            return _stagedPackageRepository
                .GetAll()
                .Where(candidate => candidate.PackageKey == packageKey)
                .OrderByDescending(candidate => candidate.Key)
                .FirstOrDefault();
        }

        private StagedPackage GetCurrentAttempt(Package package)
        {
            if (package == null)
            {
                return null;
            }

            return GetCurrentAttempt(package.Key);
        }

        private static PackageStagingResult CreateExistingPackageConflict(string id, NuGetVersion version)
        {
            return PackageStagingResult.Error(HttpStatusCode.Conflict, string.Format(Strings.PackageExistsAndCannotBeModified, id, version.ToNormalizedString()));
        }

        private ApiScopeEvaluationResult EvaluateAuthorization(User currentUser, IReadOnlyCollection<Scope> scopes, PackageRegistration packageRegistration, string id)
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
            StagedPackage stagedPackage,
            Package candidatePackage,
            PackageArchiveReader packageReader,
            PackageMetadata packageMetadata,
            PackageStreamMetadata streamMetadata,
            User currentUser)
        {
            var package = stagedPackage.Package;
            var listed = package.Listed;
            _packageService.ReplacePackageMetadataForStagedPackage(stagedPackage, packageReader, packageMetadata, streamMetadata, currentUser);
            package.PackageRegistration = candidatePackage.PackageRegistration;
            if (stagedPackage.Status != StagedPackageStatus.Deleted)
            {
                package.Listed = listed;
            }
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
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    // Save the insert without committing the transaction so SQL assigns the exact attempt key before enqueueing validation.
                    await _stagedPackageRepository.CommitChangesAsync();

                    var status = await _stagedValidationMessageEmitter.StartValidationAsync(stagedPackage);
                    if (status != stagedPackage.Status)
                    {
                        // Asynchronous validation remains Validating; immediate validation returns Ready and requires another save.
                        stagedPackage.Status = status;
                        await _stagedPackageRepository.CommitChangesAsync();
                    }
                });
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

        private static bool IsInvalidPackage(Exception exception)
        {
            return exception is InvalidPackageException
                || exception is InvalidDataException
                || exception is PackagingException
                || exception is EntityException;
        }

        private sealed class PreparedPackageUpload : IDisposable
        {
            public PreparedPackageUpload(Stream packageFile, PackageArchiveReader packageReader, PackageMetadata packageMetadata)
            {
                PackageFile = packageFile;
                PackageReader = packageReader;
                PackageMetadata = packageMetadata;
            }

            public string Id => PackageMetadata.Id;

            public string NormalizedVersion => PackageMetadata.Version.ToNormalizedString();

            public Stream PackageFile { get; }

            public PackageArchiveReader PackageReader { get; }

            public PackageMetadata PackageMetadata { get; }

            public void Dispose()
            {
                PackageReader.Dispose();
                PackageFile.Dispose();
            }
        }

        private sealed class StagingTarget
        {
            public StagingTarget(
                PackageRegistration packageRegistration,
                Package existingPackage,
                StagedPackage currentAttempt,
                User owner)
            {
                PackageRegistration = packageRegistration;
                ExistingPackage = existingPackage;
                CurrentAttempt = currentAttempt;
                Owner = owner;
            }

            public PackageRegistration PackageRegistration { get; }

            public Package ExistingPackage { get; }

            public StagedPackage CurrentAttempt { get; }

            public User Owner { get; }
        }
    }
}
