// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Packaging;
using static Lucene.Net.Search.FieldValueHitQueue;

namespace NuGetGallery
{
    public class StagingService : IStagingService
    {
        internal const int DefaultArtifactLimit = 350;
        internal static readonly TimeSpan StagingLifetime = TimeSpan.FromDays(30);
        private static readonly Regex GroupIdPattern = new Regex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageUploadService _packageUploadService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IApiScopeEvaluator _apiScopeEvaluator;
        private readonly IStagingBlobService _blobService;
        private readonly IStagingValidationMessageEmitter _validationMessageEmitter;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IMessageService _messageService;
        private readonly IMessageServiceConfiguration _messageServiceConfiguration;

        public StagingService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageUploadService packageUploadService,
            ISymbolPackageService symbolPackageService,
            IReservedNamespaceService reservedNamespaceService,
            IApiScopeEvaluator apiScopeEvaluator, IStagingBlobService blobService,
            IStagingValidationMessageEmitter validationMessageEmitter,
            IDateTimeProvider dateTimeProvider,
            IMessageService messageService,
            IMessageServiceConfiguration messageServiceConfiguration)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageUploadService = packageUploadService ?? throw new ArgumentNullException(nameof(packageUploadService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _blobService = blobService ?? throw new ArgumentNullException(nameof(blobService));
            _validationMessageEmitter = validationMessageEmitter ?? throw new ArgumentNullException(nameof(validationMessageEmitter));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _messageServiceConfiguration = messageServiceConfiguration ?? throw new ArgumentNullException(nameof(messageServiceConfiguration));
        }

        public async Task<StagingUploadResult> UploadAsync(User currentUser, User owner, Credential credential, StagingUploadRequest request)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            ValidateUploadRequest(request);

            using (var package = await PreparePackageAsync(request.Package, currentUser))
            using (var symbols = await PrepareSymbolsAsync(request.Symbols))
            {
                var upload = CreateUploadPlan(currentUser, owner, credential, request, package, symbols);
                if (!upload.HasChanges)
                {
                    var resource = StagingResourceBuilder.CreatePackage(upload.InitialState.Entry, owner, upload.PackageOperation, upload.SymbolsOperation);
                    return new StagingUploadResult(resource, created: false);
                }

                try
                {
                    await CreateUploadBlobsAsync(upload);
                    return await CommitUploadAsync(currentUser, owner, credential, request, upload);
                }
                catch
                {
                    if (!upload.Committed)
                    {
                        await DeleteUploadBlobsAsync(upload.Blobs);
                    }

                    throw;
                }
            }
        }

        private static void ValidateUploadRequest(StagingUploadRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Package == null && request.Symbols == null)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidMultipart, "At least one package or symbols artifact is required.");
            }

            if (request.Package == null && request.Listed.HasValue)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidMultipart, "The listed field requires a package artifact.", "listed");
            }

            if (request.GroupId != null && (request.GroupId.Length > 64 || !GroupIdPattern.IsMatch(request.GroupId)))
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidGroupId, "The group ID is invalid.", "groupId");
            }
        }

        private PreparedUpload CreateUploadPlan(User currentUser, User owner, Credential credential, StagingUploadRequest request, PreparedArtifact package, PreparedArtifact symbols)
        {
            EnsureMatchingIdentity(package, symbols);

            var identity = package ?? symbols;
            var state = LoadStateSnapshot(owner, identity.Id, identity.NormalizedVersion, request.GroupId);
            EnsureAuthorized(currentUser, owner, credential, state.PackageRegistration, identity.Id, symbolsOnly: package == null);
            EnsureParentAndConflict(state, owner, package != null, symbols != null);
            EnsurePromotionNotClaimed(state);

            var packageOperation = GetOperation(package, state.Entry?.PackageArtifact?.ContentHash);
            var symbolsOperation = GetOperation(symbols, state.Entry?.SymbolArtifact?.ContentHash);
            EnsureQuota(owner, packageOperation, symbolsOperation);

            var listedChanged = package != null && request.Listed.HasValue && state.Package != null && state.Package.Listed != request.Listed.Value;
            var groupChanged = request.GroupId != null && state.Entry?.StagingGroupKey != state.Group?.Key;

            return new PreparedUpload(package, symbols, state, packageOperation, symbolsOperation, listedChanged, groupChanged);
        }

        private async Task CreateUploadBlobsAsync(PreparedUpload upload)
        {
            var blobs = new List<StagingBlobReference>();
            upload.Blobs = blobs;
            if (upload.PackageChanged)
            {
                blobs.Add(await CreateBlobAsync(upload.Package.Stream, StagingBlobType.Nupkg));
            }

            if (upload.SymbolsChanged)
            {
                blobs.Add(await CreateBlobAsync(upload.Symbols.Stream, StagingBlobType.Snupkg));
            }
        }

        private async Task<StagingUploadResult> CommitUploadAsync(User currentUser, User owner, Credential credential, StagingUploadRequest request, PreparedUpload upload)
        {
            using (new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                await _entitiesContext.GetDatabase().ExecuteSqlCommandAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");

                var state = LoadState(owner, upload.Identity.Id, upload.Identity.NormalizedVersion, request.GroupId);
                EnsureAuthorized(currentUser, owner, credential, state.PackageRegistration, upload.Identity.Id, symbolsOnly: upload.Package == null);
                EnsureParentAndConflict(state, owner, upload.Package != null, upload.Symbols != null);
                EnsurePromotionNotClaimed(state);
                EnsureStateStillMatches(upload, state);
                EnsureQuota(owner, upload.PackageOperation, upload.SymbolsOperation);

                var listedChanged = upload.Package != null && request.Listed.HasValue && state.Package != null && state.Package.Listed != request.Listed.Value;
                var groupChanged = request.GroupId != null && state.Entry?.StagingGroupKey != state.Group?.Key;

                var now = _dateTimeProvider.UtcNow;
                var created = state.Entry == null;
                if (created)
                {
                    state.Entry = new StagingEntry
                    {
                        Owner = owner,
                        OwnerKey = owner.Key,
                        Package = state.Package,
                        CreatedDate = now,
                        ExpirationDate = now.Add(StagingLifetime),
                        StagingGroup = state.Group,
                        StagingGroupKey = state.Group?.Key,
                    };
                    _entitiesContext.StagingEntries.Add(state.Entry);
                }
                else if (groupChanged)
                {
                    state.Entry.StagingGroup = state.Group;
                    state.Entry.StagingGroupKey = state.Group?.Key;
                }

                if (upload.PackageChanged)
                {
                    await ApplyPackageAsync(state, upload.Package, upload.PackageBlob, currentUser, owner, request.Listed, now);
                }
                else if (listedChanged)
                {
                    state.Package.Listed = request.Listed.Value;
                }

                var revalidateRetainedSymbols = upload.PackageChanged && state.Entry.SymbolArtifact != null && !upload.SymbolsChanged;
                if (upload.SymbolsChanged || revalidateRetainedSymbols)
                {
                    ApplySymbols(state, upload.Symbols, upload.SymbolsBlob, now);
                }

                if (upload.PackageChanged || upload.SymbolsChanged)
                {
                    RefreshExpiration(state.Entry, now);
                }

                await _entitiesContext.SaveChangesAsync();

                if (upload.PackageChanged)
                {
                    await DispatchValidationAsync(state.Package, state.Entry.PackageArtifact.ValidationTrackingId);
                }

                if (upload.SymbolsChanged || revalidateRetainedSymbols)
                {
                    await DispatchValidationAsync(state.Entry.SymbolArtifact.SymbolPackage, state.Entry.SymbolArtifact.ValidationTrackingId);
                }

                transaction.Commit();
                upload.Committed = true;

                var resource = StagingResourceBuilder.CreatePackage(state.Entry, owner, upload.PackageOperation, upload.SymbolsOperation);
                if (upload.PackageChanged || upload.SymbolsChanged)
                {
                    await SendUploadDigestAsync(owner, resource);
                }

                return new StagingUploadResult(resource, created);
            }
        }

        private static bool IsContentChange(StagingArtifactOperation? operation)
        {
            return operation == StagingArtifactOperation.Created || operation == StagingArtifactOperation.Replaced;
        }

        private async Task<PreparedArtifact> PreparePackageAsync(Stream stream, User currentUser)
        {
            if (stream == null)
            {
                return null;
            }

            var artifact = await PrepareArtifactAsync(stream, "package");
            try
            {
                await _packageService.EnsureValid(artifact.Archive);
                artifact.PackageMetadata = PackageMetadata.FromNuspecReader(artifact.Archive.GetNuspecReader(), strict: true);

                var validation = await _packageUploadService.ValidateBeforeGeneratePackageAsync(artifact.Archive, artifact.PackageMetadata, currentUser);
                if (validation.Type != PackageValidationResultType.Accepted)
                {
                    throw InvalidPackage(validation.Message.PlainTextMessage, "package");
                }

                return artifact;
            }
            catch (StagingApiException)
            {
                artifact.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is InvalidPackageException || ex is InvalidDataException || ex is PackagingException || ex is EntityException)
            {
                artifact.Dispose();
                throw InvalidPackage(ex.Message, "package");
            }
            catch
            {
                artifact.Dispose();
                throw;
            }
        }

        private async Task<PreparedArtifact> PrepareSymbolsAsync(Stream stream)
        {
            if (stream == null)
            {
                return null;
            }

            var artifact = await PrepareArtifactAsync(stream, "symbols");
            try
            {
                await _symbolPackageService.EnsureValidAsync(artifact.Archive);
                return artifact;
            }
            catch (StagingApiException)
            {
                artifact.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is InvalidPackageException || ex is InvalidDataException || ex is PackagingException || ex is EntityException)
            {
                artifact.Dispose();
                throw InvalidPackage(ex.Message, "symbols");
            }
            catch
            {
                artifact.Dispose();
                throw;
            }
        }

        private static async Task<PreparedArtifact> PrepareArtifactAsync(Stream stream, string target)
        {
            try
            {
                var seekable = stream.AsSeekableStream();
                var invalidEntry = ZipArchiveHelpers.ValidateArchiveEntries(seekable, out ZipArchiveEntry _);
                if (invalidEntry != InvalidZipEntry.None)
                {
                    throw InvalidPackage("The archive contains an invalid entry.", target);
                }

                var archive = new PackageArchiveReader(seekable, leaveStreamOpen: true);
                if (PackageValidationHelper.HasDuplicatedEntries(archive))
                {
                    archive.Dispose();
                    throw InvalidPackage("The archive contains duplicate entries.", target);
                }

                var nuspec = archive.GetNuspecReader();
                var version = nuspec.GetVersion();
                var hash = CryptographyService.GenerateHash(seekable, CoreConstants.Sha512HashAlgorithmId);
                seekable.Position = 0;

                return new PreparedArtifact(seekable, archive, nuspec.GetId(), version, hash);
            }
            catch (StagingApiException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidPackageException || ex is InvalidDataException || ex is PackagingException)
            {
                throw InvalidPackage(ex.Message, target);
            }
        }

        private UploadState LoadStateSnapshot(User owner, string id, string normalizedVersion, string groupId)
        {
            return LoadStateCore(owner, id, normalizedVersion, groupId, asNoTracking: true);
        }

        private UploadState LoadState(User owner, string id, string normalizedVersion, string groupId)
        {
            return LoadStateCore(owner, id, normalizedVersion, groupId, asNoTracking: false);
        }

        private UploadState LoadStateCore(User owner, string id, string normalizedVersion, string groupId, bool asNoTracking)
        {
            var packageQuery = _entitiesContext.Packages
                .Include(x => x.PackageRegistration.Owners)
                .Include(x => x.SymbolPackages);
            if (asNoTracking)
            {
                packageQuery = packageQuery.AsNoTracking();
            }

            var package = packageQuery.SingleOrDefault(x => x.PackageRegistration.Id == id && x.NormalizedVersion == normalizedVersion);
            var registration = package?.PackageRegistration;
            if (registration == null)
            {
                var registrationQuery = _entitiesContext.PackageRegistrations.Include(x => x.Owners);
                if (asNoTracking)
                {
                    registrationQuery = registrationQuery.AsNoTracking();
                }

                registration = registrationQuery.SingleOrDefault(x => x.Id == id);
            }

            StagingEntry entry = null;
            if (package != null)
            {
                var entryQuery = _entitiesContext.StagingEntries
                    .Include(x => x.Package)
                    .Include(x => x.PackageArtifact)
                    .Include(x => x.SymbolArtifact.SymbolPackage)
                    .Include(x => x.StagingGroup);
                if (asNoTracking)
                {
                    entryQuery = entryQuery.AsNoTracking();
                }

                entry = entryQuery.SingleOrDefault(x => x.PackageKey == package.Key);
            }

            var group = entry?.StagingGroup;
            if (groupId != null)
            {
                var groupQuery = _entitiesContext.StagingGroups.AsQueryable();
                if (asNoTracking)
                {
                    groupQuery = groupQuery.AsNoTracking();
                }

                group = groupQuery.SingleOrDefault(x => x.OwnerKey == owner.Key && x.Id == groupId);
            }

            if (groupId != null && group == null)
            {
                throw Error(HttpStatusCode.NotFound, StagingApiErrorCodes.GroupNotFound, "The staging group was not found.", "groupId");
            }

            return new UploadState
            {
                Package = package,
                PackageRegistration = registration,
                Entry = entry,
                Group = group,
            };
        }

        private void EnsureAuthorized(User currentUser, User owner, Credential credential, PackageRegistration registration, string id, bool symbolsOnly)
        {
            ApiScopeEvaluationResult evaluation;
            if (registration == null)
            {
                var context = new ActionOnNewPackageContext(id, _reservedNamespaceService);
                evaluation = _apiScopeEvaluator.Evaluate(currentUser, credential.Scopes, ActionsRequiringPermissions.UploadNewPackageId, context, NuGetScopes.PackageStage);
            }
            else
            {
                evaluation = _apiScopeEvaluator.Evaluate(currentUser, credential.Scopes, ActionsRequiringPermissions.UploadNewPackageVersion, registration, NuGetScopes.PackageStage);
            }

            if (!evaluation.ScopesAreValid || evaluation.Owner?.Key != owner.Key)
            {
                throw Error(HttpStatusCode.Forbidden, StagingApiErrorCodes.StagingScopeRequired, "The package:stage credential does not allow this package ID.");
            }

            if (!evaluation.IsSuccessful())
            {
                if (symbolsOnly)
                {
                    throw Error(HttpStatusCode.NotFound, StagingApiErrorCodes.ParentNotFound, "An available or paired staged parent package was not found.");
                }

                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.PackageVersionConflict, "The package ID or version cannot be staged by this owner.");
            }

            if (registration?.IsLocked == true)
            {
                if (symbolsOnly)
                {
                    throw Error(HttpStatusCode.NotFound, StagingApiErrorCodes.ParentNotFound, "An eligible parent package was not found.");
                }

                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.PackageVersionConflict, "The package ID is locked and cannot be staged.");
            }
        }

        private void EnsurePromotionNotClaimed(UploadState state)
        {
            var entryIsPromoting = state.Entry?.PackageArtifact?.PromotionArtifactHistoryKey != null || state.Entry?.SymbolArtifact?.PromotionArtifactHistoryKey != null;
            if (entryIsPromoting)
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.StagedPackagePromoting, "The staged package has already been claimed for promotion.");
            }

            var currentGroupKey = state.Entry?.StagingGroupKey;
            var requestedGroupKey = state.Group?.Key;
            var groupKeys = new[] { currentGroupKey, requestedGroupKey };

            var groupIsPromoting = _entitiesContext.StagingPromotionHistories.Any(x =>
                x.GroupKey.HasValue
                && groupKeys.Contains(x.GroupKey)
                && x.Status == StagingPromotionHistoryStatus.InProgress);
            if (groupIsPromoting)
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.GroupPromoting, "The staging group is being promoted.");
            }
        }

        private static void EnsureParentAndConflict(UploadState state, User owner, bool hasPackage, bool hasSymbols)
        {
            if (state.Entry != null && state.Entry.OwnerKey != owner.Key)
            {
                if (!hasPackage && hasSymbols)
                {
                    throw Error(HttpStatusCode.NotFound, StagingApiErrorCodes.ParentNotFound, "An available or paired staged parent package was not found.");
                }

                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.PackageVersionConflict, "The package version is already staged by another owner.");
            }

            if (hasPackage && state.Package != null && (state.Entry == null || state.Package.PackageStatusKey != PackageStatus.Staged))
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.PackageVersionConflict, "The package version already exists.");
            }

            if (!hasPackage && hasSymbols)
            {
                var hasEligibleParent = state.Package != null
                    && (state.Entry?.PackageArtifact != null
                        || state.Package.PackageStatusKey == PackageStatus.Available
                        || state.Package.PackageStatusKey == PackageStatus.Validating
                        || state.Package.PackageStatusKey == PackageStatus.FailedValidation);
                if (!hasEligibleParent)
                {
                    throw Error(HttpStatusCode.NotFound, StagingApiErrorCodes.ParentNotFound, "An eligible parent package was not found.");
                }
            }
        }

        private static void EnsureMatchingIdentity(PreparedArtifact package, PreparedArtifact symbols)
        {
            if (package == null || symbols == null)
            {
                return;
            }

            if (!string.Equals(package.Id, symbols.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(package.NormalizedVersion, symbols.NormalizedVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.ArtifactIdentityMismatch, "The nupkg and snupkg must have the same ID and version.", "symbols");
            }
        }

        private void EnsureQuota(User owner, StagingArtifactOperation? packageOperation, StagingArtifactOperation? symbolsOperation)
        {
            var additionalArtifacts = 0;
            if (packageOperation == StagingArtifactOperation.Created)
            {
                additionalArtifacts++;
            }

            if (symbolsOperation == StagingArtifactOperation.Created)
            {
                additionalArtifacts++;
            }

            if (additionalArtifacts == 0)
            {
                return;
            }

            var usedArtifacts = _entitiesContext.StagedPackageArtifacts.Count(x => x.StagingEntry.OwnerKey == owner.Key) +
                _entitiesContext.StagedSymbolArtifacts.Count(x => x.StagingEntry.OwnerKey == owner.Key);
            var limit = owner.StagingArtifactLimit ?? DefaultArtifactLimit;
            if (usedArtifacts + additionalArtifacts > limit)
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.ArtifactQuotaExceeded, "The staging artifact quota has been exceeded.");
            }
        }

        private static StagingArtifactOperation? GetOperation(PreparedArtifact artifact, string existingHash)
        {
            if (artifact == null)
            {
                return null;
            }

            if (existingHash == null)
            {
                return StagingArtifactOperation.Created;
            }

            return string.Equals(artifact.ContentHash, existingHash, StringComparison.Ordinal) ? StagingArtifactOperation.Unchanged : StagingArtifactOperation.Replaced;
        }

        private static void EnsureStateStillMatches(PreparedUpload upload, UploadState state)
        {
            var packageHash = state.Entry?.PackageArtifact?.ContentHash;
            var symbolsHash = state.Entry?.SymbolArtifact?.ContentHash;
            var packageMatches = ArtifactStateMatches(upload.PackageOperation, upload.InitialPackageHash, GetOperation(upload.Package, packageHash), packageHash);
            var symbolsMatch = ArtifactStateMatches(upload.SymbolsOperation, upload.InitialSymbolsHash, GetOperation(upload.Symbols, symbolsHash), symbolsHash);
            if (!packageMatches || !symbolsMatch)
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.PackageVersionConflict, "The staged package changed during the upload.");
            }
        }

        private static bool ArtifactStateMatches(StagingArtifactOperation? expectedOperation, string expectedHash, StagingArtifactOperation? actualOperation, string actualHash)
        {
            return expectedOperation == actualOperation && string.Equals(expectedHash, actualHash, StringComparison.Ordinal);
        }

        private async Task ApplyPackageAsync(UploadState state, PreparedArtifact artifact, StagingBlobReference blob, User currentUser, User owner, bool? listed, DateTime now)
        {
            var isNewPackage = state.Package == null;
            var listedIntent = listed ?? state.Package?.Listed ?? true;
            if (isNewPackage)
            {
                state.Package = await _packageUploadService.GeneratePackageAsync(artifact.Id, artifact.Archive, CreateMetadata(artifact), owner, currentUser);
                state.Entry.Package = state.Package;
            }
            else
            {
                ClearPackageCollections(state.Package);
                state.Package = _packageService.EnrichPackageFromNuGetPackage(state.Package, artifact.Archive, artifact.PackageMetadata, CreateMetadata(artifact), currentUser);
            }

            state.Package.PackageStatusKey = PackageStatus.Staged;
            state.Package.Listed = listedIntent;
            state.Package.LastEdited = now;

            var isNewPackageRegistration = state.PackageRegistration == null;
            var afterValidation = await _packageUploadService.ValidateAfterGeneratePackageAsync(state.Package, artifact.Archive, owner, currentUser, isNewPackageRegistration);
            if (afterValidation.Type != PackageValidationResultType.Accepted)
            {
                throw InvalidPackage(afterValidation.Message.PlainTextMessage, "package");
            }

            await _packageService.UpdateIsLatestAsync(state.Package.PackageRegistration, commitChanges: false);

            if (state.Entry.PackageArtifact == null)
            {
                state.Entry.PackageArtifact = new StagedPackageArtifact
                {
                    StagingEntry = state.Entry,
                };
            }
            else
            {
                QueueCleanup(state.Entry.PackageArtifact.BlobPath, state.Entry.PackageArtifact.BlobETag);
            }

            state.Entry.PackageArtifact.BlobPath = blob.BlobPath;
            state.Entry.PackageArtifact.BlobETag = blob.ETag;
            state.Entry.PackageArtifact.ContentHash = blob.ContentHash;
            state.Entry.PackageArtifact.Status = StagingArtifactStatus.Validating;
            state.Entry.PackageArtifact.ValidationTrackingId = Guid.NewGuid();
            state.Entry.PackageArtifact.UploadedDate = now;
            state.Entry.PackageArtifact.ValidatedDate = null;
        }

        private void ApplySymbols(UploadState state, PreparedArtifact artifact, StagingBlobReference blob, DateTime now)
        {
            if (blob != null)
            {
                var oldSymbolPackage = state.Entry.SymbolArtifact?.SymbolPackage;
                var symbolPackage = _symbolPackageService.CreateSymbolPackage(state.Package, CreateMetadata(artifact));
                symbolPackage.StatusKey = PackageStatus.Staged;
                symbolPackage.Published = null;

                if (oldSymbolPackage != null)
                {
                    _entitiesContext.DeleteOnCommit(oldSymbolPackage);
                    QueueCleanup(state.Entry.SymbolArtifact.BlobPath, state.Entry.SymbolArtifact.BlobETag);
                }

                if (state.Entry.SymbolArtifact == null)
                {
                    state.Entry.SymbolArtifact = new StagedSymbolArtifact
                    {
                        StagingEntry = state.Entry,
                    };
                }

                state.Entry.SymbolArtifact.SymbolPackage = symbolPackage;
                state.Entry.SymbolArtifact.BlobPath = blob.BlobPath;
                state.Entry.SymbolArtifact.BlobETag = blob.ETag;
                state.Entry.SymbolArtifact.ContentHash = blob.ContentHash;
                state.Entry.SymbolArtifact.UploadedDate = now;
            }

            var symbols = state.Entry.SymbolArtifact;
            symbols.ParentContentHash = state.Entry.PackageArtifact?.ContentHash ?? state.Package.Hash;
            symbols.Status = StagingArtifactStatus.Validating;
            symbols.ValidationTrackingId = Guid.NewGuid();
            symbols.ValidatedDate = null;

        }

        private void ClearPackageCollections(Package package)
        {
            foreach (var item in package.SupportedFrameworks.ToList())
            {
                _entitiesContext.Set<PackageFramework>().Remove(item);
            }
            package.SupportedFrameworks.Clear();

#pragma warning disable 618
            foreach (var item in package.Authors.ToList())
            {
                _entitiesContext.Set<PackageAuthor>().Remove(item);
            }
            package.Authors.Clear();
#pragma warning restore 618

            foreach (var item in package.Dependencies.ToList())
            {
                _entitiesContext.Set<NuGet.Services.Entities.PackageDependency>().Remove(item);
            }
            package.Dependencies.Clear();

            foreach (var item in package.PackageTypes.ToList())
            {
                _entitiesContext.Set<NuGet.Services.Entities.PackageType>().Remove(item);
            }
            package.PackageTypes.Clear();
        }

        private static void RefreshExpiration(StagingEntry entry, DateTime now)
        {
            var expiration = now.Add(StagingLifetime);
            if (entry.StagingGroup == null)
            {
                entry.ExpirationDate = expiration;
            }
            else
            {
                entry.StagingGroup.ExpirationDate = expiration;
            }
        }

        private async Task<StagingBlobReference> CreateBlobAsync(Stream stream, StagingBlobType blobType)
        {
            stream.Position = 0;
            try
            {
                return await _blobService.CreateAsync(stream, blobType);
            }
            catch (Exception ex) when (ex is CloudBlobStorageException || ex is FileAlreadyExistsException)
            {
                throw Error(HttpStatusCode.ServiceUnavailable, StagingApiErrorCodes.StagingUnavailable, "Staging storage is temporarily unavailable.");
            }
        }

        private async Task DeleteUploadBlobsAsync(IReadOnlyCollection<StagingBlobReference> blobs)
        {
            foreach (var blob in blobs)
            {
                try
                {
                    await _blobService.DeleteAsync(blob.BlobPath, blob.ETag);
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }
        }

        private async Task DispatchValidationAsync(Package package, Guid trackingId)
        {
            try
            {
                await _validationMessageEmitter.StartValidationAsync(package, trackingId);
            }
            catch (Exception ex) when (ex is ServiceBusException || ex is ReadOnlyModeException)
            {
                throw Error(HttpStatusCode.ServiceUnavailable, StagingApiErrorCodes.StagingUnavailable, "Staging validation messaging is temporarily unavailable.");
            }
        }

        private async Task DispatchValidationAsync(SymbolPackage package, Guid trackingId)
        {
            try
            {
                await _validationMessageEmitter.StartValidationAsync(package, trackingId);
            }
            catch (Exception ex) when (ex is ServiceBusException || ex is ReadOnlyModeException)
            {
                throw Error(HttpStatusCode.ServiceUnavailable, StagingApiErrorCodes.StagingUnavailable, "Staging validation messaging is temporarily unavailable.");
            }
        }

        private async Task SendUploadDigestAsync(User owner, StagedPackageResource resource)
        {
            try
            {
                await _messageService.SendMessageAsync(new StagingUploadMessage(_messageServiceConfiguration, owner, resource));
            }
            catch (ServiceBusException ex)
            {
                ex.Log();
            }
        }

        private void QueueCleanup(string blobPath, string expectedETag)
        {
            _entitiesContext.StagingBlobCleanups.Add(new StagingBlobCleanup
            {
                BlobPath = blobPath,
                ExpectedETag = expectedETag,
                CreatedDate = _dateTimeProvider.UtcNow,
            });
        }

        private static PackageStreamMetadata CreateMetadata(PreparedArtifact artifact)
        {
            return new PackageStreamMetadata
            {
                HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                Hash = artifact.ContentHash,
                Size = artifact.Stream.Length,
            };
        }

        private static StagingApiException InvalidPackage(string message, string target)
        {
            return Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidPackage, message, target);
        }

        private static StagingApiException Error(HttpStatusCode statusCode, string code, string message, string target = null)
        {
            return new StagingApiException(statusCode, code, message, target);
        }

        private sealed class PreparedUpload
        {
            public PreparedUpload(PreparedArtifact package, PreparedArtifact symbols, UploadState initialState, StagingArtifactOperation? packageOperation,
                StagingArtifactOperation? symbolsOperation, bool listedChanged, bool groupChanged)
            {
                Package = package;
                Symbols = symbols;
                InitialState = initialState;
                PackageOperation = packageOperation;
                SymbolsOperation = symbolsOperation;
                ListedChanged = listedChanged;
                GroupChanged = groupChanged;
                InitialPackageHash = initialState.Entry?.PackageArtifact?.ContentHash;
                InitialSymbolsHash = initialState.Entry?.SymbolArtifact?.ContentHash;
            }

            public PreparedArtifact Package { get; }
            public PreparedArtifact Symbols { get; }
            public PreparedArtifact Identity => Package ?? Symbols;
            public UploadState InitialState { get; }
            public StagingArtifactOperation? PackageOperation { get; }
            public StagingArtifactOperation? SymbolsOperation { get; }
            public bool PackageChanged => IsContentChange(PackageOperation);
            public bool SymbolsChanged => IsContentChange(SymbolsOperation);
            public bool ListedChanged { get; }
            public bool GroupChanged { get; }
            public bool HasChanges => PackageChanged || SymbolsChanged || ListedChanged || GroupChanged;
            public bool Committed { get; set; }
            public string InitialPackageHash { get; }
            public string InitialSymbolsHash { get; }
            public IReadOnlyCollection<StagingBlobReference> Blobs { get; set; }
            public StagingBlobReference PackageBlob => Blobs.SingleOrDefault(x => x.BlobType == StagingBlobType.Nupkg);
            public StagingBlobReference SymbolsBlob => Blobs.SingleOrDefault(x => x.BlobType == StagingBlobType.Snupkg);
        }

        private sealed class PreparedArtifact : IDisposable
        {
            public PreparedArtifact(Stream stream, PackageArchiveReader archive, string id, NuGetVersion version, string contentHash)
            {
                Stream = stream;
                Archive = archive;
                Id = id;
                Version = version;
                ContentHash = contentHash;
            }

            public Stream Stream { get; }
            public PackageArchiveReader Archive { get; }
            public string Id { get; }
            public NuGetVersion Version { get; }
            public string NormalizedVersion => Version.ToNormalizedString();
            public string ContentHash { get; }
            public PackageMetadata PackageMetadata { get; set; }

            public void Dispose()
            {
                Archive.Dispose();
            }
        }

        private sealed class UploadState
        {
            public Package Package { get; set; }
            public PackageRegistration PackageRegistration { get; set; }
            public StagingEntry Entry { get; set; }
            public StagingGroup Group { get; set; }
        }
    }
}
