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
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;
using NuGetGallery.Security;

namespace NuGetGallery
{
    /// <summary>
    /// Stages symbol packages for available or same-owner staged parent packages.
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
        private readonly IPackageStagingManagementService _managementService;
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly IAppConfiguration _configuration;
        private readonly IStagingQuotaService _quotaService;

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
            IStagedSymbolPackageValidationMessageEmitter validationMessageEmitter,
            IPackageStagingManagementService managementService,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            IAppConfiguration configuration,
            IStagingQuotaService quotaService)
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
            _managementService = managementService ?? throw new ArgumentNullException(nameof(managementService));
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        }

        public async Task<PackageStagingResult> StageSymbolPackageAsync(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            HttpContextBase httpContext,
            Stream symbolPackageFile,
            string groupId = null)
        {
            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            return await ProcessUploadAsync(currentUser, scopes, httpContext, symbolPackageFile, groupId, authorizedAttempt: null);
        }

        public Task<PackageStagingResult> ReplaceSymbolPackageAsync(
            User currentUser,
            HttpContextBase httpContext,
            StagedSymbolPackage stagedSymbolPackage,
            Stream symbolPackageFile)
        {
            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            return ProcessUploadAsync(currentUser, scopes: null, httpContext, symbolPackageFile, groupId: null, authorizedAttempt: stagedSymbolPackage);
        }

        private async Task<PackageStagingResult> ProcessUploadAsync(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            HttpContextBase httpContext,
            Stream symbolPackageFile,
            string groupId,
            StagedSymbolPackage authorizedAttempt)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (symbolPackageFile == null)
            {
                throw new ArgumentNullException(nameof(symbolPackageFile));
            }

            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (groupId != null && string.IsNullOrWhiteSpace(groupId))
            {
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, "The group ID must not be empty.");
            }

            if (authorizedAttempt == null)
            {
                var userPolicyResult = await _securityPolicyService.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, httpContext);
                if (!userPolicyResult.Success)
                {
                    return PackageStagingResult.Error(HttpStatusCode.BadRequest, userPolicyResult.ErrorMessage);
                }
            }

            var owner = authorizedAttempt?.StagedPackageIdentity.Owner ?? _authorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
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
                if (authorizedAttempt != null)
                {
                    var expectedPackage = authorizedAttempt.StagedPackageIdentity.Package;
                    var hasMatchingId = string.Equals(expectedPackage.Id, nuspec.GetId(), StringComparison.OrdinalIgnoreCase);
                    var hasMatchingVersion = string.Equals(expectedPackage.NormalizedVersion, nuspec.GetVersion().ToNormalizedString(), StringComparison.OrdinalIgnoreCase);
                    if (!hasMatchingId || !hasMatchingVersion)
                    {
                        return PackageStagingResult.Error(HttpStatusCode.BadRequest, "The replacement symbol package identity does not match the staged symbol package.");
                    }
                }

                var package = _packageService.FindPackageByIdAndVersionStrict(nuspec.GetId(), nuspec.GetVersion().ToStringSafe());
                var targetError = ValidateTarget(currentUser, scopes, owner, package, authorizedAttempt);
                if (targetError != null)
                {
                    return targetError;
                }

                StagingGroup group = null;
                if (groupId != null)
                {
                    group = _managementService.FindStagingGroup(owner, groupId) ?? new StagingGroup
                    {
                        Owner = owner,
                        OwnerKey = owner.Key,
                        Id = groupId,
                        Name = groupId,
                        CreatedDate = DateTime.UtcNow,
                        ExpirationDate = StagingExpirationPolicy.CreateDeadline(_configuration),
                    };
                    if (group.ActivePromotionId.HasValue)
                    {
                        return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staging group is being promoted.");
                    }
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

                var identity = GetIdentity(package) ?? new StagedPackageIdentity
                {
                    Package = package,
                    Owner = owner,
                    OwnerKey = owner.Key,
                };
                var previousAttempt = identity.CurrentStagedSymbolPackage;
                StagingExpirationPolicy.EnsureMutable(identity, group, includePackage: false);
                if ((previousAttempt?.Status == StagedPackageStatus.Validating || previousAttempt?.Status == StagedPackageStatus.Ready || previousAttempt?.Status == StagedPackageStatus.WaitingForParent) && previousAttempt.SymbolPackage.Hash == hash)
                {
                    if (group != null && identity.StagingGroupKey != group.Key)
                    {
                        try
                        {
                            await _stagedSymbolPackageRepository.ExecuteInTransactionAsync(async () =>
                            {
                                StagingExpirationPolicy.EnsureMutable(identity, group, includePackage: false);
                                if (package.PackageStatusKey == PackageStatus.Staged)
                                {
                                    identity.CurrentStagedPackage.MutationRevision++;
                                }

                                previousAttempt.MutationRevision++;
                                StagingGroupAssignment.Update(identity, group, _stagingGroupRepository, StagingExpirationPolicy.CreateDeadline(_configuration));
                                await _stagedSymbolPackageRepository.CommitChangesAsync();
                            });
                        }
                        catch (DbUpdateException exception)
                        {
                            exception.Log();
                            return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staged symbol package changed during upload. Retry the upload.");
                        }
                    }

                    return PackageStagingResult.Ok();
                }

                if (previousAttempt?.SymbolPackage.StatusKey != PackageStatus.Staged)
                {
                    await _quotaService.EnsureCapacityAsync(owner);
                }

                file.Position = 0;
                var blob = await _stagingBlobService.SaveSymbolPackageFileAsync(package.Id, package.NormalizedVersion, file);
                var symbolPackage = _symbolPackageService.CreateSymbolPackage(package, metadata);
                symbolPackage.StatusKey = PackageStatus.Staged;

                var stagedSymbolPackage = new StagedSymbolPackage
                {
                    SymbolPackage = symbolPackage,
                    StagedPackageIdentity = identity,
                    UploadedBlobPath = blob.Path,
                    UploadedBlobETag = blob.ETag,
                    UploadedDate = DateTime.UtcNow,
                    ExpirationDate = StagingExpirationPolicy.CreateDeadline(_configuration),
                    Status = package.PackageStatusKey == PackageStatus.Deleted ? StagedPackageStatus.WaitingForParent : StagedPackageStatus.Validating,
                };
                _stagedSymbolPackageRepository.InsertOnCommit(stagedSymbolPackage);

                try
                {
                    await _stagedSymbolPackageRepository.ExecuteInTransactionAsync(async () =>
                    {
                        StagingExpirationPolicy.EnsureMutable(identity, group, includePackage: false);
                        if (previousAttempt != null)
                        {
                            previousAttempt.Status = StagedPackageStatus.Superseded;
                        }

                        if (package.PackageStatusKey == PackageStatus.Staged)
                        {
                            identity.CurrentStagedPackage.MutationRevision++;
                        }

                        StagingGroupAssignment.Update(identity, group, _stagingGroupRepository, stagedSymbolPackage.ExpirationDate);
                        StagingExpirationPolicy.RefreshGroup(identity.StagingGroup, stagedSymbolPackage.ExpirationDate);
                        await _stagedSymbolPackageRepository.CommitChangesAsync();
                        identity.CurrentStagedSymbolPackageKey = stagedSymbolPackage.Key;
                        identity.CurrentStagedSymbolPackage = stagedSymbolPackage;
                        await _stagedSymbolPackageRepository.CommitChangesAsync();
                        if (stagedSymbolPackage.Status == StagedPackageStatus.Validating)
                        {
                            stagedSymbolPackage.Status = await _validationMessageEmitter.StartValidationAsync(stagedSymbolPackage);
                            await _stagedSymbolPackageRepository.CommitChangesAsync();
                        }
                    });
                }
                catch (DbUpdateException exception)
                {
                    exception.Log();
                    return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staged symbol package changed during upload. Retry the upload.");
                }

                if (previousAttempt != null)
                {
                    return PackageStagingResult.Ok();
                }

                return PackageStagingResult.Created(warnings: null);
            }
            catch (Exception exception) when (IsInvalidPackage(exception))
            {
                exception.Log();
                return PackageStagingResult.Error(HttpStatusCode.BadRequest, exception.Message);
            }
            catch (StagingExpiredException exception)
            {
                exception.Log();
                return PackageStagingResult.Error(HttpStatusCode.Conflict, exception.Message);
            }
            catch (StagingQuotaExceededException exception)
            {
                exception.Log();
                return PackageStagingResult.Error(HttpStatusCode.Conflict, exception.Message);
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
                Status = StagingExpirationPolicy.HasExpired(stagedSymbolPackage) ? "Expired" : stagedSymbolPackage.Status.ToString(),
                Expires = StagingExpirationPolicy.GetDeadline(stagedSymbolPackage).ToUtcIso8601String(),
            };
        }

        private PackageStagingResult ValidateTarget(
            User currentUser,
            IReadOnlyCollection<Scope> scopes,
            User owner,
            Package package,
            StagedSymbolPackage authorizedAttempt)
        {
            if (package == null || (authorizedAttempt == null && !CanAccessPackage(currentUser, scopes, owner, package)))
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The parent package was not found.");
            }

            var identity = GetIdentity(package);
            if (authorizedAttempt != null && (identity?.CurrentStagedSymbolPackageKey != authorizedAttempt.Key || !_authorizationService.CanManage(currentUser, authorizedAttempt)))
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The staged symbol package was not found.");
            }

            if (identity != null && identity.OwnerKey != owner.Key)
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The parent package was not found.");
            }

            var parent = identity?.CurrentStagedPackage;
            var stagedParent = package.PackageStatusKey == PackageStatus.Staged && parent != null && parent.Status != StagedPackageStatus.Deleted && parent.Status != StagedPackageStatus.Superseded;
            var retainedSymbols = package.PackageStatusKey == PackageStatus.Deleted && parent?.Status == StagedPackageStatus.Deleted && identity.CurrentStagedSymbolPackageKey.HasValue;
            if (package.PackageStatusKey != PackageStatus.Available && !stagedParent && !retainedSymbols)
            {
                return PackageStagingResult.Error(HttpStatusCode.NotFound, "The parent package was not found.");
            }

            if (parent?.Status == StagedPackageStatus.Promoting)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, "The parent package is being promoted.");
            }

            if (identity?.StagingGroup?.ActivePromotionId.HasValue == true)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staging group is being promoted.");
            }

            if (identity?.CurrentStagedSymbolPackage?.Status == StagedPackageStatus.Promoting)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staged symbol package is being promoted.");
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
                .Include(identity => identity.CurrentStagedPackage)
                .Include(identity => identity.CurrentStagedSymbolPackage.SymbolPackage)
                .Include(identity => identity.StagingGroup)
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
