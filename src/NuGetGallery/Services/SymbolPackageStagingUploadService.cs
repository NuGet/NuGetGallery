// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;
using NuGetGallery.Security;

namespace NuGetGallery
{
    /// <summary>
    /// Stages symbol packages for available parent packages.
    /// </summary>
    public class SymbolPackageStagingUploadService : ISymbolPackageStagingUploadService
    {
        private readonly IApiScopeEvaluator _apiScopeEvaluator;
        private readonly IContentObjectService _contentObjectService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageStagingAuthorizationService _authorizationService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly ISecurityPolicyService _securityPolicyService;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly IEntityRepository<StagedSymbolPackage> _stagedSymbolPackageRepository;
        private readonly IStagedSymbolPackageValidationMessageEmitter _validationMessageEmitter;

        public SymbolPackageStagingUploadService(
            IApiScopeEvaluator apiScopeEvaluator,
            IContentObjectService contentObjectService,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageStagingAuthorizationService authorizationService,
            ISymbolPackageService symbolPackageService,
            ISecurityPolicyService securityPolicyService,
            IStagingBlobService stagingBlobService,
            IEntityRepository<StagedSymbolPackage> stagedSymbolPackageRepository,
            IStagedSymbolPackageValidationMessageEmitter validationMessageEmitter)
        {
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _stagedSymbolPackageRepository = stagedSymbolPackageRepository ?? throw new ArgumentNullException(nameof(stagedSymbolPackageRepository));
            _validationMessageEmitter = validationMessageEmitter ?? throw new ArgumentNullException(nameof(validationMessageEmitter));
        }

        public async Task<PackageStagingResult> StageSymbolPackageAsync(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            HttpContextBase httpContext,
            Stream symbolPackageFile)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (symbolPackageFile == null)
            {
                throw new ArgumentNullException(nameof(symbolPackageFile));
            }

            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var userPolicyResult = await _securityPolicyService.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, httpContext);
            if (!userPolicyResult.Success)
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, userPolicyResult.ErrorMessage);
            }

            var owner = _authorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (owner == null || !_contentObjectService.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(currentUser))
            {
                return PackageStagingResult.Error(HttpStatusCode.Forbidden, Strings.SymbolsPackage_UploadNotAllowed);
            }

            using var file = symbolPackageFile.AsSeekableStream();
            try
            {
                var validationError = ZipArchiveHelpers.GetArchiveValidationError(file);
                if (validationError != null)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, validationError);
                }

                using var archive = new PackageArchiveReader(file, leaveStreamOpen: true);
                var nuspec = archive.GetNuspecReader();
                var package = _packageService.FindPackageByIdAndVersionStrict(nuspec.GetId(), nuspec.GetVersion().ToStringSafe());
                var targetError = ValidateTarget(currentUser, scopes, owner, package);
                if (targetError != null)
                {
                    return targetError;
                }

                if (PackageValidationHelper.HasDuplicatedEntries(archive))
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, Strings.UploadPackage_PackageContainsDuplicatedEntries);
                }

                await _symbolPackageService.EnsureValidAsync(archive);
                var hash = CryptographyService.GenerateHash(file, CoreConstants.Sha512HashAlgorithmId);
                var metadata = new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = hash,
                    Size = file.Length,
                };

                file.Position = 0;
                var blob = await _stagingBlobService.SaveSymbolPackageFileAsync(package.Id, package.NormalizedVersion, file);
                var symbolPackage = _symbolPackageService.CreateSymbolPackage(package, metadata);
                symbolPackage.StatusKey = PackageStatus.Staged;

                var identity = GetIdentity(package) ?? new StagedPackageIdentity
                {
                    Package = package,
                    Owner = owner,
                    OwnerKey = owner.Key,
                };
                var stagedSymbolPackage = new StagedSymbolPackage
                {
                    SymbolPackage = symbolPackage,
                    StagedPackageIdentity = identity,
                    UploadedBlobPath = blob.Path,
                    UploadedBlobETag = blob.ETag,
                    UploadedDate = DateTime.UtcNow,
                    Status = StagedPackageStatus.Validating,
                };
                _stagedSymbolPackageRepository.InsertOnCommit(stagedSymbolPackage);

                try
                {
                    await _stagedSymbolPackageRepository.ExecuteInTransactionAsync(async () =>
                    {
                        await _stagedSymbolPackageRepository.CommitChangesAsync();
                        identity.CurrentStagedSymbolPackageKey = stagedSymbolPackage.Key;
                        identity.CurrentStagedSymbolPackage = stagedSymbolPackage;
                        await _stagedSymbolPackageRepository.CommitChangesAsync();
                        stagedSymbolPackage.Status = await _validationMessageEmitter.StartValidationAsync(stagedSymbolPackage);
                        await _stagedSymbolPackageRepository.CommitChangesAsync();
                    });
                }
                catch (DbUpdateException exception)
                {
                    exception.Log();
                    return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staged symbol package changed during upload. Retry the upload.");
                }

                return PackageStagingResult.Created(warnings: null);
            }
            catch (Exception exception) when (IsInvalidPackage(exception))
            {
                exception.Log();
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, exception.Message);
            }
        }

        public SymbolPackageStagingStatus GetStatus(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            string id,
            string version)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(version));
            }

            var owner = _authorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (owner == null)
            {
                return null;
            }

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (!CanAccessPackage(currentUser, scopes, owner, package))
            {
                return null;
            }

            var stagedSymbolPackage = _stagedSymbolPackageRepository.GetAll()
                .SingleOrDefault(candidate =>
                    candidate.StagedPackageIdentityKey == package.Key &&
                    candidate.StagedPackageIdentity.OwnerKey == owner.Key &&
                    candidate.StagedPackageIdentity.CurrentStagedSymbolPackageKey == candidate.Key);
            if (stagedSymbolPackage == null)
            {
                return null;
            }

            return new SymbolPackageStagingStatus
            {
                Id = package.Id,
                Version = package.NormalizedVersion,
                Status = stagedSymbolPackage.Status.ToString(),
            };
        }

        private PackageStagingResult ValidateTarget(User currentUser, IReadOnlyCollection<Scope> scopes, User owner, Package package)
        {
            if (package?.PackageStatusKey != PackageStatus.Available || !CanAccessPackage(currentUser, scopes, owner, package))
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The available parent package was not found.");
            }

            var identity = GetIdentity(package);
            if (identity != null && identity.OwnerKey != owner.Key)
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The available parent package was not found.");
            }

            if (identity?.CurrentStagedSymbolPackageKey != null)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, "A staged symbol package already exists for this package.");
            }

            if (_entitiesContext.SymbolPackages.Any(candidate => candidate.PackageKey == package.Key && candidate.StatusKey == PackageStatus.Validating))
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, Strings.SymbolsPackage_ConflictValidating);
            }

            return null;
        }

        private bool CanAccessPackage(User currentUser, IReadOnlyCollection<Scope> scopes, User owner, Package package)
        {
            if (package == null)
            {
                return false;
            }

            var authorization = _apiScopeEvaluator.Evaluate(
                currentUser,
                scopes,
                ActionsRequiringPermissions.UploadSymbolPackage,
                package.PackageRegistration,
                NuGetScopes.PackageStage);
            return authorization.IsSuccessful() && authorization.Owner.Key == owner.Key;
        }

        private StagedPackageIdentity GetIdentity(Package package)
        {
            return _entitiesContext.StagedPackageIdentities
                .SingleOrDefault(candidate => candidate.Key == package.Key);
        }

        private static bool IsInvalidPackage(Exception exception)
        {
            return exception is InvalidPackageException
                || exception is InvalidDataException
                || exception is EntityException
                || exception is FrameworkException;
        }
    }
}
