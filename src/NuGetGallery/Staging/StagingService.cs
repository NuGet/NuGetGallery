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
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Packaging;
using static Lucene.Net.Search.FieldValueHitQueue;

namespace NuGetGallery
{
    public class StagingService : IStagingService
    {
        internal const int DefaultArtifactLimit = 350;
        internal const int DefaultGroupLimit = 100;
        internal const int DefaultListTake = 100;
        internal const int MaxListTake = 500;
        internal const int EntryScanBatchSize = MaxListTake + 1;
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
        private readonly IAppConfiguration _appConfiguration;
        private readonly IStagingTokenProtector _tokenProtector;

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
            IMessageServiceConfiguration messageServiceConfiguration,
            IAppConfiguration appConfiguration,
            IStagingTokenProtector tokenProtector)
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
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
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
                    var entryGalleryUrl = BuildEntryGalleryUrl(upload.InitialState.Entry);
                    var promotionGalleryUrl = BuildPromotionGalleryUrl(upload.InitialState.Entry);
                    var entry = StagingResourceBuilder.CreatePackage(upload.InitialState.Entry, owner, entryGalleryUrl, promotionGalleryUrl, upload.PackageOperation, upload.SymbolsOperation);
                    return new StagingUploadResult(entry, created: false);
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

                var resource = StagingResourceBuilder.CreatePackage(state.Entry, owner, BuildEntryGalleryUrl(state.Entry), BuildPromotionGalleryUrl(state.Entry), upload.PackageOperation, upload.SymbolsOperation);
                if (upload.PackageChanged || upload.SymbolsChanged)
                {
                    await SendUploadDigestAsync(owner, resource);
                }

                return new StagingUploadResult(resource, created);
            }
        }

        public StagedPackageResource GetPackage(StagingAuthorizationContext context, string id, string version)
        {
            EnsureCanActForOwner(context);
            var entry = FindEntryOrThrow(context, id, version, tracking: false);
            return BuildPackageResource(entry, context.Owner);
        }

        public StagedPackageListResource ListPackages(StagingAuthorizationContext context, string groupId, bool ungrouped, int take, string continuationToken)
        {
            EnsureCanActForOwner(context);
            if (groupId != null && ungrouped)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidFilterCombination, "Supply either a group filter or ungrouped, not both.");
            }

            int? groupKey = null;
            if (groupId != null)
            {
                var group = FindGroupOrThrow(context, groupId, tracking: false);
                EnsureGroupVisibleOrEmpty(context, group.Key);
                groupKey = group.Key;
            }

            var effectiveTake = ClampTake(take);
            var filter = StagingContinuationToken.DescribeFilter(groupId, ungrouped);
            var cursor = StagingContinuationToken.Decode(_tokenProtector, continuationToken, context.Owner.Key, filter);
            var page = LoadEntryPage(context, groupKey, ungrouped, cursor, filter, effectiveTake);

            return new StagedPackageListResource
            {
                Owner = context.Owner.Username,
                Quota = BuildQuota(context.Owner),
                Packages = page.Resources,
                ContinuationToken = page.ContinuationToken,
            };
        }

        public async Task<StagingDownloadResult> DownloadPackageAsync(StagingAuthorizationContext context, string id, string version)
        {
            EnsureCanActForOwner(context);
            var entry = FindEntryOrThrow(context, id, version, tracking: false);
            if (entry.PackageArtifact == null)
            {
                throw StagedPackageNotFound();
            }

            var artifact = entry.PackageArtifact;
            var reference = new StagingBlobReference(artifact.BlobPath, artifact.BlobETag, artifact.ContentHash, entry.Package.PackageFileSize, StagingBlobType.Nupkg);
            var stream = await OpenBlobAsync(reference);
            var fileName = $"{entry.Package.PackageRegistration.Id}.{entry.Package.NormalizedVersion}.nupkg";
            return new StagingDownloadResult(stream, fileName, CoreConstants.OctetStreamContentType);
        }

        public async Task<StagingDownloadResult> DownloadSymbolsAsync(StagingAuthorizationContext context, string id, string version)
        {
            EnsureCanActForOwner(context);
            var entry = FindEntryOrThrow(context, id, version, tracking: false);
            if (entry.SymbolArtifact == null)
            {
                throw StagedPackageNotFound();
            }

            var artifact = entry.SymbolArtifact;
            var reference = new StagingBlobReference(artifact.BlobPath, artifact.BlobETag, artifact.ContentHash, artifact.SymbolPackage.FileSize, StagingBlobType.Snupkg);
            var stream = await OpenBlobAsync(reference);
            var fileName = $"{entry.Package.PackageRegistration.Id}.{entry.Package.NormalizedVersion}.snupkg";
            return new StagingDownloadResult(stream, fileName, CoreConstants.OctetStreamContentType);
        }

        public Task<StagedPackageResource> SetListedAsync(StagingAuthorizationContext context, string id, string version, bool listed)
        {
            EnsureCanActForOwner(context);
            return InTransactionAsync(async () =>
            {
                var entry = FindEntryOrThrow(context, id, version, tracking: true);
                if (entry.PackageArtifact == null)
                {
                    // A symbols-only staged package references its non-staged parent and cannot change the parent's listing state.
                    throw StagedPackageNotFound();
                }

                EnsureEntryNotPromoting(entry);
                EnsureGroupNotPromoting(entry.StagingGroupKey);

                entry.Package.Listed = listed;
                entry.Package.LastEdited = _dateTimeProvider.UtcNow;

                await _entitiesContext.SaveChangesAsync();
                return BuildPackageResource(entry, context.Owner);
            });
        }

        public Task DeletePackageAsync(StagingAuthorizationContext context, string id, string version)
        {
            EnsureCanActForOwner(context);
            return InTransactionAsync(async () =>
            {
                var entry = FindEntryOrThrow(context, id, version, tracking: true);
                EnsureEntryNotPromoting(entry);
                EnsureGroupNotPromoting(entry.StagingGroupKey);

                if (entry.SymbolArtifact != null)
                {
                    RemoveStagedSymbolsContent(entry);
                }

                if (entry.PackageArtifact != null)
                {
                    RemoveStagedPackageContent(entry);
                }

                _entitiesContext.StagingEntries.Remove(entry);

                await _entitiesContext.SaveChangesAsync();
                return true;
            });
        }

        public Task DeleteSymbolsAsync(StagingAuthorizationContext context, string id, string version)
        {
            EnsureCanActForOwner(context);
            return InTransactionAsync(async () =>
            {
                var entry = FindEntryOrThrow(context, id, version, tracking: true);
                if (entry.SymbolArtifact == null)
                {
                    throw StagedPackageNotFound();
                }

                EnsureArtifactNotPromoting(entry.SymbolArtifact);
                EnsureGroupNotPromoting(entry.StagingGroupKey);

                RemoveStagedSymbolsContent(entry);

                if (entry.PackageArtifact == null)
                {
                    // Symbols were the entry's last staged artifact; the now-empty entry is deleted, leaving the
                    // staged or referenced parent untouched.
                    _entitiesContext.StagingEntries.Remove(entry);
                }

                await _entitiesContext.SaveChangesAsync();
                return true;
            });
        }

        public Task<StagingGroupResult> CreateGroupAsync(StagingAuthorizationContext context, StagingCreateGroupRequest request)
        {
            EnsureCanActForOwner(context, groupScope: true);
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateGroupId(request.Id);
            var name = NormalizeGroupName(request.Name, request.Id);
            var ownerKey = context.Owner.Key;

            return InTransactionAsync(async () =>
            {
                var existing = _entitiesContext.StagingGroups.FirstOrDefault(x => x.OwnerKey == ownerKey && x.Id == request.Id);
                if (existing != null)
                {
                    // Apply the same visibility rules as group reads: a group whose members are all outside this
                    // credential's subjects must not be distinguishable from absence.
                    EnsureGroupVisibleOrEmpty(context, existing.Key);

                    // Idempotent recreate only when the requested (normalized) name equals the stored name. A different
                    // name is a conflict rather than a silent rename.
                    if (!string.Equals(name, existing.Name, StringComparison.Ordinal))
                    {
                        throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.GroupAlreadyExists, "A staging group with this ID already exists with a different name.", "id");
                    }

                    return new StagingGroupResult(BuildGroupSummaryForGroup(context, existing), created: false);
                }

                var groupCount = _entitiesContext.StagingGroups.Count(x => x.OwnerKey == ownerKey);
                if (groupCount >= DefaultGroupLimit)
                {
                    throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.GroupLimitExceeded, "The staging group limit has been reached.");
                }

                var now = _dateTimeProvider.UtcNow;
                var group = new StagingGroup
                {
                    Owner = context.Owner,
                    OwnerKey = ownerKey,
                    Id = request.Id,
                    Name = name,
                    CreatedDate = now,
                    ExpirationDate = now.Add(StagingLifetime),
                };
                _entitiesContext.StagingGroups.Add(group);

                await _entitiesContext.SaveChangesAsync();
                return new StagingGroupResult(BuildGroupSummaryForGroup(context, group), created: true);
            });
        }

        public IReadOnlyList<StagingGroupResource> ListGroups(StagingAuthorizationContext context)
        {
            EnsureCanActForOwner(context, groupScope: true);
            var ownerKey = context.Owner.Key;
            var groups = _entitiesContext.StagingGroups
                .AsNoTracking()
                .Where(x => x.OwnerKey == ownerKey)
                .ToList();

            // One owner-wide lean projection of every grouped entry, then build all summaries in memory. This avoids
            // both O(groups) round-trips and loading the full include graph for every member.
            var groupedEntries = EntryProjectionQuery(ownerKey)
                .Where(x => x.GroupKey != null)
                .ToList();
            var membersByGroup = groupedEntries
                .GroupBy(x => x.GroupKey.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<EntryProjection>)g.ToList());

            var summaries = new List<StagingGroupResource>();
            foreach (var group in groups.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                var members = membersByGroup.TryGetValue(group.Key, out var found) ? found : Array.Empty<EntryProjection>();
                var visibleMembers = members.Where(x => CredentialAllowsPackageId(context, x.PackageId)).ToList();

                // No disclosure: hide a non-empty group when the credential cannot see any of its members.
                if (members.Count > 0 && visibleMembers.Count == 0)
                {
                    continue;
                }

                summaries.Add(BuildGroupSummaryFromProjections(group, context.Owner, visibleMembers));
            }

            return summaries;
        }

        public StagingGroupDetailResource GetGroup(StagingAuthorizationContext context, string groupId, int take, string continuationToken)
        {
            EnsureCanActForOwner(context, groupScope: true);
            var group = FindGroupOrThrow(context, groupId, tracking: false);

            // Lean group-scoped projection drives the summary, visibility, and page-key selection; only the selected
            // page keys then load their full entry graphs for the response.
            var visibleMembers = LoadVisibleGroupProjections(context, group.Key, out var memberCount);

            // No disclosure: a non-empty group with no members visible to the credential is treated as not found.
            if (memberCount > 0 && visibleMembers.Count == 0)
            {
                throw GroupNotFound();
            }

            var effectiveTake = ClampTake(take);
            var filter = StagingContinuationToken.DescribeFilter(groupId, ungrouped: false);
            var cursor = StagingContinuationToken.Decode(_tokenProtector, continuationToken, context.Owner.Key, filter);

            var ordered = visibleMembers.OrderBy(x => x.Key).AsEnumerable();
            if (cursor != null)
            {
                ordered = ordered.Where(x => x.Key > cursor.LastKey);
            }

            var pageKeys = ordered.Take(effectiveTake + 1).Select(x => x.Key).ToList();
            string nextToken = null;
            if (pageKeys.Count > effectiveTake)
            {
                var boundaryKey = pageKeys[effectiveTake - 1];
                nextToken = new StagingContinuationToken { OwnerKey = context.Owner.Key, Filter = filter, LastKey = boundaryKey }.Encode(_tokenProtector);
                pageKeys = pageKeys.Take(effectiveTake).ToList();
            }

            var pageEntries = LoadEntriesByKeys(pageKeys).Select(x => BuildPackageResource(x, context.Owner)).ToList();

            var summary = BuildGroupSummaryFromProjections(group, context.Owner, visibleMembers);
            return new StagingGroupDetailResource
            {
                Id = summary.Id,
                Name = summary.Name,
                Owner = summary.Owner,
                Created = summary.Created,
                Expires = summary.Expires,
                PackageCount = summary.PackageCount,
                ArtifactCount = summary.ArtifactCount,
                Status = summary.Status,
                CanPromote = summary.CanPromote,
                GalleryUrl = summary.GalleryUrl,
                Packages = pageEntries,
                ContinuationToken = nextToken,
            };
        }

        public Task<StagingGroupResource> RenameGroupAsync(StagingAuthorizationContext context, string groupId, StagingRenameGroupRequest request)
        {
            EnsureCanActForOwner(context, groupScope: true);
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var newName = ValidateRenameName(request);

            return InTransactionAsync(async () =>
            {
                var group = FindGroupOrThrow(context, groupId, tracking: true);
                var members = EntryProjectionQuery(context.Owner.Key).Where(x => x.GroupKey == group.Key).ToList();
                EnsureAllMembersVisibleProjection(context, members);
                EnsureGroupNotPromoting(group.Key);
                group.Name = newName;

                await _entitiesContext.SaveChangesAsync();
                return BuildGroupSummaryFromProjections(group, context.Owner, members);
            });
        }

        public Task DeleteGroupAsync(StagingAuthorizationContext context, string groupId)
        {
            EnsureCanActForOwner(context, groupScope: true);
            return InTransactionAsync(async () =>
            {
                var group = FindGroupOrThrow(context, groupId, tracking: true);
                var entries = LoadGroupEntries(context.Owner.Key, group.Key, tracking: true);
                EnsureAllMembersVisible(context, entries);
                EnsureGroupNotPromoting(group.Key);

                foreach (var entry in entries)
                {
                    EnsureEntryNotPromoting(entry);
                    if (entry.SymbolArtifact != null)
                    {
                        RemoveStagedSymbolsContent(entry);
                    }

                    if (entry.PackageArtifact != null)
                    {
                        RemoveStagedPackageContent(entry);
                    }

                    _entitiesContext.StagingEntries.Remove(entry);
                }

                _entitiesContext.StagingGroups.Remove(group);

                await _entitiesContext.SaveChangesAsync();
                return true;
            });
        }

        public Task<StagedPackageResource> AddPackageToGroupAsync(StagingAuthorizationContext context, string groupId, string id, string version)
        {
            EnsureCanActForOwner(context);
            return InTransactionAsync(async () =>
            {
                var group = FindGroupOrThrow(context, groupId, tracking: true);

                // Apply the hidden-group rule to the destination: a narrow credential must not probe or mutate a group
                // whose members are all outside its subjects. Empty and partially-visible destinations remain allowed.
                EnsureGroupVisibleOrEmpty(context, group.Key);

                var entry = FindEntryOrThrow(context, id, version, tracking: true);
                EnsureEntryNotPromoting(entry);
                EnsureGroupNotPromoting(group.Key);
                EnsureGroupNotPromoting(entry.StagingGroupKey);

                if (entry.StagingGroupKey == group.Key)
                {
                    // Idempotent: already a member of the target group. Membership and expiration are unchanged.
                    return BuildPackageResource(entry, context.Owner);
                }

                entry.StagingGroup = group;
                entry.StagingGroupKey = group.Key;

                await _entitiesContext.SaveChangesAsync();
                return BuildPackageResource(entry, context.Owner);
            });
        }

        public Task<StagedPackageResource> RemovePackageFromGroupAsync(StagingAuthorizationContext context, string groupId, string id, string version)
        {
            EnsureCanActForOwner(context);
            return InTransactionAsync(async () =>
            {
                var group = FindGroupOrThrow(context, groupId, tracking: true);
                var entry = FindEntryOrThrow(context, id, version, tracking: true);
                if (entry.StagingGroupKey != group.Key)
                {
                    throw StagedPackageNotFound();
                }

                EnsureEntryNotPromoting(entry);
                EnsureGroupNotPromoting(group.Key);

                // Removing copies the group's current deadline onto the now-ungrouped entry.
                entry.ExpirationDate = group.ExpirationDate;
                entry.StagingGroup = null;
                entry.StagingGroupKey = null;

                await _entitiesContext.SaveChangesAsync();
                return BuildPackageResource(entry, context.Owner);
            });
        }

        private StagedPackageResource BuildPackageResource(StagingEntry entry, User owner)
        {
            return StagingResourceBuilder.CreatePackage(entry, owner, BuildEntryGalleryUrl(entry), BuildPromotionGalleryUrl(entry));
        }

        private StagingGroupResource BuildGroupSummaryForGroup(StagingAuthorizationContext context, StagingGroup group)
        {
            var visibleMembers = LoadVisibleGroupProjections(context, group.Key, out _);
            return BuildGroupSummaryFromProjections(group, context.Owner, visibleMembers);
        }

        private StagingGroupResource BuildGroupSummaryFromProjections(StagingGroup group, User owner, IReadOnlyList<EntryProjection> visibleMembers)
        {
            var artifactCount = visibleMembers.Sum(x => (x.HasPackageArtifact ? 1 : 0) + (x.HasSymbolArtifact ? 1 : 0));
            var status = AggregateProjectionStatuses(owner.Key, visibleMembers);
            return StagingResourceBuilder.CreateGroupSummary(group, owner, visibleMembers.Count, artifactCount, status, BuildGroupGalleryUrl(group.Id));
        }

        private static string AggregateProjectionStatuses(int ownerKey, IReadOnlyList<EntryProjection> entries)
        {
            bool AnyArtifactIs(StagingArtifactStatus status) => entries.Any(x => x.PackageArtifactStatus == status || x.SymbolArtifactStatus == status);
            bool AnyReferenceParentIs(PackageStatus status) => entries.Any(x => !x.HasPackageArtifact && x.ReferencePackageStatus == status);

            if (AnyArtifactIs(StagingArtifactStatus.Promoting))
            {
                return StagingResourceValues.StatusPromoting;
            }

            if (AnyArtifactIs(StagingArtifactStatus.PromotionFailed))
            {
                return StagingResourceValues.StatusPromotionFailed;
            }

            if (AnyArtifactIs(StagingArtifactStatus.ValidationFailed) || AnyReferenceParentIs(PackageStatus.FailedValidation))
            {
                return StagingResourceValues.StatusValidationFailed;
            }

            if (AnyArtifactIs(StagingArtifactStatus.Validating) || AnyReferenceParentIs(PackageStatus.Validating))
            {
                return StagingResourceValues.StatusValidating;
            }

            var ownershipRemoved = entries.Any(x => !x.OwnerOwnsRegistration);
            var referenceBlocked = entries.Any(x => !x.HasPackageArtifact
                && x.ReferencePackageStatus != PackageStatus.Available
                && x.ReferencePackageStatus != PackageStatus.Validating
                && x.ReferencePackageStatus != PackageStatus.FailedValidation);
            if (ownershipRemoved || referenceBlocked)
            {
                return StagingResourceValues.StatusBlocked;
            }

            return StagingResourceValues.StatusReady;
        }

        private StagingQuotaResource BuildQuota(User owner)
        {
            var usedArtifacts = _entitiesContext.StagedPackageArtifacts.Count(x => x.StagingEntry.OwnerKey == owner.Key) +
                _entitiesContext.StagedSymbolArtifacts.Count(x => x.StagingEntry.OwnerKey == owner.Key);
            return new StagingQuotaResource
            {
                UsedArtifacts = usedArtifacts,
                Limit = owner.StagingArtifactLimit ?? DefaultArtifactLimit,
            };
        }

        private EntryPage LoadEntryPage(StagingAuthorizationContext context, int? groupKey, bool ungrouped, StagingContinuationToken cursor, string filter, int take)
        {
            var ownerKey = context.Owner.Key;
            var allInclusive = IsAllInclusiveStageCredential(context);

            // Two-phase, bounded scan: page over lean key/ID projections in key order, filtering subjects in batches
            // until take + 1 visible keys are found or the input is exhausted. Only the selected page keys then have
            // their full entry graphs loaded. The cursor stores the last included key so the next page resumes forward
            // without rescanning earlier entries and without omitting a visible entry.
            var lastScannedKey = cursor?.LastKey ?? 0;
            var visibleKeys = new List<int>(take + 1);

            while (visibleKeys.Count <= take)
            {
                var lowerBound = lastScannedKey;
                var batch = EntryKeyProjectionQuery(ownerKey, groupKey, ungrouped)
                    .Where(x => x.Key > lowerBound)
                    .OrderBy(x => x.Key)
                    .Take(EntryScanBatchSize)
                    .ToList();
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var candidate in batch)
                {
                    lastScannedKey = candidate.Key;
                    if (allInclusive || CredentialAllowsPackageId(context, candidate.PackageId))
                    {
                        visibleKeys.Add(candidate.Key);
                        if (visibleKeys.Count > take)
                        {
                            break;
                        }
                    }
                }

                if (batch.Count < EntryScanBatchSize)
                {
                    break;
                }
            }

            string nextToken = null;
            if (visibleKeys.Count > take)
            {
                var boundaryKey = visibleKeys[take - 1];
                nextToken = new StagingContinuationToken { OwnerKey = ownerKey, Filter = filter, LastKey = boundaryKey }.Encode(_tokenProtector);
                visibleKeys = visibleKeys.Take(take).ToList();
            }

            var resources = LoadEntriesByKeys(visibleKeys).Select(x => BuildPackageResource(x, context.Owner)).ToList();
            return new EntryPage(resources, nextToken);
        }

        private List<StagingEntry> LoadEntriesByKeys(IReadOnlyCollection<int> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return new List<StagingEntry>();
            }

            var keySet = keys.ToList();
            return EntryQuery(tracking: false)
                .Where(x => keySet.Contains(x.Key))
                .OrderBy(x => x.Key)
                .ToList();
        }

        private IReadOnlyList<EntryProjection> LoadVisibleGroupProjections(StagingAuthorizationContext context, int groupKey, out int memberCount)
        {
            var members = EntryProjectionQuery(context.Owner.Key).Where(x => x.GroupKey == groupKey).ToList();
            memberCount = members.Count;
            return members.Where(x => CredentialAllowsPackageId(context, x.PackageId)).ToList();
        }

        private IQueryable<EntryProjection> EntryProjectionQuery(int ownerKey)
        {
            // Lean projection carrying only the fields needed for counts, status precedence, canPromote, ownership
            // visibility, and subject filtering. It deliberately avoids the full include graph so group summaries and
            // visibility checks do not materialize every member's registration, artifacts, symbol package, and
            // promotion history.
            return _entitiesContext.StagingEntries
                .AsNoTracking()
                .Where(x => x.OwnerKey == ownerKey)
                .Select(x => new EntryProjection
                {
                    Key = x.Key,
                    GroupKey = x.StagingGroupKey,
                    PackageId = x.Package.PackageRegistration.Id,
                    HasPackageArtifact = x.PackageArtifact != null,
                    PackageArtifactStatus = x.PackageArtifact != null ? (StagingArtifactStatus?)x.PackageArtifact.Status : null,
                    HasSymbolArtifact = x.SymbolArtifact != null,
                    SymbolArtifactStatus = x.SymbolArtifact != null ? (StagingArtifactStatus?)x.SymbolArtifact.Status : null,
                    ReferencePackageStatus = x.Package.PackageStatusKey,
                    OwnerOwnsRegistration = x.Package.PackageRegistration.Owners.Any(o => o.Key == ownerKey),
                });
        }

        private IQueryable<EntryKeyProjection> EntryKeyProjectionQuery(int ownerKey, int? groupKey, bool ungrouped)
        {
            var query = _entitiesContext.StagingEntries.AsNoTracking().Where(x => x.OwnerKey == ownerKey);
            if (groupKey.HasValue)
            {
                query = query.Where(x => x.StagingGroupKey == groupKey.Value);
            }
            else if (ungrouped)
            {
                query = query.Where(x => x.StagingGroupKey == null);
            }

            return query.Select(x => new EntryKeyProjection { Key = x.Key, PackageId = x.Package.PackageRegistration.Id });
        }

        private List<StagingEntry> LoadGroupEntries(int ownerKey, int groupKey, bool tracking)
        {
            return EntryQuery(tracking)
                .Where(x => x.OwnerKey == ownerKey && x.StagingGroupKey == groupKey)
                .OrderBy(x => x.Key)
                .ToList();
        }

        private static void EnsureAllMembersVisible(StagingAuthorizationContext context, IReadOnlyList<StagingEntry> members)
        {
            // A group-wide mutation (rename/delete) must be authorized for every current member; otherwise the group
            // is not disclosed. This prevents a narrow-subject credential from renaming or deleting a group whose
            // members include packages it cannot manage.
            if (members.Any(x => !CredentialAllowsPackageId(context, x.Package.PackageRegistration.Id)))
            {
                throw GroupNotFound();
            }
        }

        private static void EnsureAllMembersVisibleProjection(StagingAuthorizationContext context, IReadOnlyList<EntryProjection> members)
        {
            if (members.Any(x => !CredentialAllowsPackageId(context, x.PackageId)))
            {
                throw GroupNotFound();
            }
        }

        private void EnsureGroupVisibleOrEmpty(StagingAuthorizationContext context, int groupKey)
        {
            // A narrow-subject credential must not distinguish a group whose members are all outside its subjects from
            // an absent group. Empty groups remain visible (owner-scoped metadata with no package identity).
            if (IsAllInclusiveStageCredential(context))
            {
                return;
            }

            var visible = LoadVisibleGroupProjections(context, groupKey, out var memberCount);
            if (memberCount > 0 && visible.Count == 0)
            {
                throw GroupNotFound();
            }
        }

        private void EnsureEntryVisible(StagingAuthorizationContext context, StagingEntry entry)
        {
            if (!CredentialAllowsPackageId(context, entry.Package.PackageRegistration.Id))
            {
                // Do not disclose the existence of a staged package outside the credential's permitted package subjects.
                throw StagedPackageNotFound();
            }
        }

        private static bool CredentialAllowsPackageId(StagingAuthorizationContext context, string packageId)
        {
            // Reuses the same subject/action predicates (ScopeExtensions) that IApiScopeEvaluator applies, so a narrow
            // package:stage credential only sees the package IDs its scopes permit. Owner authorization is enforced
            // separately by the credential's owner scope and owner-scoped queries.
            return context.Scopes.Any(scope => !string.IsNullOrEmpty(scope.Subject) && scope.AllowsActions(NuGetScopes.PackageStage) && scope.AllowsSubject(packageId));
        }

        private static bool IsAllInclusiveStageCredential(StagingAuthorizationContext context)
        {
            return context.Scopes.Any(scope =>
                scope.AllowsActions(NuGetScopes.PackageStage)
                && string.Equals(scope.Subject, NuGetPackagePattern.AllInclusivePattern, StringComparison.Ordinal));
        }

        private IQueryable<StagingEntry> EntryQuery(bool tracking)
        {
            var query = _entitiesContext.StagingEntries
                .Include(x => x.Package.PackageRegistration.Owners)
                .Include(x => x.PackageArtifact.PromotionArtifactHistory.StagingPromotionHistory)
                .Include(x => x.SymbolArtifact.SymbolPackage)
                .Include(x => x.SymbolArtifact.PromotionArtifactHistory.StagingPromotionHistory)
                .Include(x => x.StagingGroup);
            return tracking ? query : query.AsNoTracking();
        }

        private StagingEntry FindEntryOrThrow(StagingAuthorizationContext context, string id, string version, bool tracking)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw StagedPackageNotFound();
            }

            var normalizedVersion = NormalizeVersionOrThrow(version);

            // Filter the registration ID in the database query (case-insensitively, matching Gallery SQL collation)
            // so only the single matching entry graph is materialized instead of every entry sharing the version.
            var loweredId = id.ToLowerInvariant();
            var entry = EntryQuery(tracking)
                .Where(x => x.OwnerKey == context.Owner.Key
                    && x.Package.NormalizedVersion == normalizedVersion
                    && x.Package.PackageRegistration.Id.ToLower() == loweredId)
                .SingleOrDefault();
            if (entry == null)
            {
                throw StagedPackageNotFound();
            }

            EnsureEntryVisible(context, entry);
            return entry;
        }

        private StagingGroup FindGroupOrThrow(StagingAuthorizationContext context, string groupId, bool tracking)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                throw GroupNotFound();
            }

            var ownerKey = context.Owner.Key;
            var loweredGroupId = groupId.ToLowerInvariant();
            var query = _entitiesContext.StagingGroups.AsQueryable();
            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            var group = query
                .Where(x => x.OwnerKey == ownerKey && x.Id.ToLower() == loweredGroupId)
                .SingleOrDefault();
            if (group == null)
            {
                throw GroupNotFound();
            }

            return group;
        }

        private void RemoveStagedSymbolsContent(StagingEntry entry)
        {
            var symbol = entry.SymbolArtifact;
            QueueCleanup(symbol.BlobPath, symbol.BlobETag);
            if (symbol.SymbolPackage != null)
            {
                _entitiesContext.DeleteOnCommit(symbol.SymbolPackage);
            }

            _entitiesContext.StagedSymbolArtifacts.Remove(symbol);
            entry.SymbolArtifact = null;
        }

        private void RemoveStagedPackageContent(StagingEntry entry)
        {
            var artifact = entry.PackageArtifact;
            QueueCleanup(artifact.BlobPath, artifact.BlobETag);
            _entitiesContext.StagedPackageArtifacts.Remove(artifact);
            entry.PackageArtifact = null;

            // Staged nupkg deletion rule: the parent Package(Status = Staged) was only ever private, so it is
            // hard-deleted along with its child rows. A symbols-only staged package never reaches this path, so its
            // referenced non-staged parent is left untouched.
            var package = entry.Package;
            if (package != null && package.PackageStatusKey == PackageStatus.Staged)
            {
                ClearPackageCollections(package);
                package.PackageRegistration?.Packages?.Remove(package);
                _entitiesContext.DeleteOnCommit(package);
            }
        }

        private void EnsureEntryNotPromoting(StagingEntry entry)
        {
            if (IsArtifactPromotionActive(entry.PackageArtifact) || IsArtifactPromotionActive(entry.SymbolArtifact))
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.StagedPackagePromoting, "The staged package is being promoted.");
            }
        }

        private void EnsureArtifactNotPromoting(StagedSymbolArtifact artifact)
        {
            if (IsArtifactPromotionActive(artifact))
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.StagedPackagePromoting, "The staged symbols are being promoted.");
            }
        }

        private void EnsureGroupNotPromoting(int? groupKey)
        {
            if (!groupKey.HasValue)
            {
                return;
            }

            var promoting = _entitiesContext.StagingPromotionHistories.Any(x => x.GroupKey == groupKey.Value && x.Status == StagingPromotionHistoryStatus.InProgress);
            if (promoting)
            {
                throw Error(HttpStatusCode.Conflict, StagingApiErrorCodes.GroupPromoting, "The staging group is being promoted.");
            }
        }

        private static bool IsArtifactPromotionActive(StagedPackageArtifact artifact)
        {
            return artifact != null && (artifact.PromotionArtifactHistoryKey != null
                || artifact.Status == StagingArtifactStatus.Promoting
                || artifact.Status == StagingArtifactStatus.PromotionFailed);
        }

        private static bool IsArtifactPromotionActive(StagedSymbolArtifact artifact)
        {
            return artifact != null && (artifact.PromotionArtifactHistoryKey != null
                || artifact.Status == StagingArtifactStatus.Promoting
                || artifact.Status == StagingArtifactStatus.PromotionFailed);
        }

        private async Task<Stream> OpenBlobAsync(StagingBlobReference reference)
        {
            try
            {
                return await _blobService.OpenReadAsync(reference);
            }
            catch (Exception ex) when (ex is CloudBlobStorageException || ex is StagingBlobIntegrityException)
            {
                throw Error(HttpStatusCode.ServiceUnavailable, StagingApiErrorCodes.StagingUnavailable, "Staging storage is temporarily unavailable.");
            }
        }

        private async Task<T> InTransactionAsync<T>(Func<Task<T>> body)
        {
            using (new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                await _entitiesContext.GetDatabase().ExecuteSqlCommandAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
                var result = await body();
                transaction.Commit();
                return result;
            }
        }

        private async Task InTransactionAsync(Func<Task<bool>> body)
        {
            await InTransactionAsync<bool>(body);
        }

        private string BuildEntryGalleryUrl(StagingEntry entry)
        {
            return entry.StagingGroup != null ? BuildGroupGalleryUrl(entry.StagingGroup.Id) : StagingBaseUrl();
        }

        private string BuildGroupGalleryUrl(string groupId)
        {
            var baseUrl = StagingBaseUrl();
            return baseUrl == null ? null : baseUrl + "/" + groupId;
        }

        private string BuildPromotionGalleryUrl(StagingEntry entry)
        {
            // The promotion recovery URL is derived from the current live promotion history record when that
            // relationship is loaded. Nothing is invented: if no promotion history is linked yet, no URL is produced.
            var history = entry.PackageArtifact?.PromotionArtifactHistory?.StagingPromotionHistory
                ?? entry.SymbolArtifact?.PromotionArtifactHistory?.StagingPromotionHistory;
            if (history == null || history.Id == Guid.Empty)
            {
                return null;
            }

            var baseUrl = StagingBaseUrl();
            return baseUrl == null ? null : baseUrl + "/promotions/" + history.Id.ToString("D");
        }

        private string StagingBaseUrl()
        {
            var siteRoot = _appConfiguration.SiteRoot;
            return string.IsNullOrEmpty(siteRoot) ? null : siteRoot.TrimEnd('/') + "/account/packages/staging";
        }

        private static int ClampTake(int take)
        {
            if (take < 1)
            {
                return DefaultListTake;
            }

            return take > MaxListTake ? MaxListTake : take;
        }

        private static string NormalizeVersionOrThrow(string version)
        {
            if (!NuGetVersion.TryParse(version, out var parsed))
            {
                throw StagedPackageNotFound();
            }

            return parsed.ToNormalizedString();
        }

        private static void ValidateGroupId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64 || !GroupIdPattern.IsMatch(id))
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidGroupId, "The group ID is invalid.", "id");
            }
        }

        private static string NormalizeGroupName(string name, string id)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return id;
            }

            if (trimmed.Length > 256)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidGroupId, "The group name is invalid.", "name");
            }

            return trimmed;
        }

        private static string ValidateRenameName(StagingRenameGroupRequest request)
        {
            // Rename requires an explicit name. Unlike create, an absent field must not silently fall back to the
            // group ID, so a missing name is rejected rather than defaulted.
            if (request?.Name == null)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidRequestBody, "A group name is required.", "name");
            }

            var trimmed = request.Name.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 256)
            {
                throw Error(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidGroupId, "The group name is invalid.", "name");
            }

            return trimmed;
        }

        private static void EnsureCanActForOwner(StagingAuthorizationContext context, bool groupScope = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Runtime account authority: the current user must still be able to act for the owner (itself or as a
            // current organization member). A removed member's still-valid key must not retain staging access. This is
            // deliberately independent of package-registration ownership so an owner can still manage staged content
            // after registration ownership changes. Failure returns a 404 without disclosing any resource.
            if (!ActionsRequiringPermissions.UploadNewPackageId.IsAllowedOnBehalfOfAccount(context.CurrentUser, context.Owner))
            {
                throw groupScope ? GroupNotFound() : StagedPackageNotFound();
            }
        }

        private static StagingApiException StagedPackageNotFound()
        {
            return Error(HttpStatusCode.NotFound, StagingApiErrorCodes.StagedPackageNotFound, "The staged package was not found.");
        }

        private static StagingApiException GroupNotFound()
        {
            return Error(HttpStatusCode.NotFound, StagingApiErrorCodes.GroupNotFound, "The staging group was not found.");
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

        private sealed class EntryPage
        {
            public EntryPage(IReadOnlyList<StagedPackageResource> resources, string continuationToken)
            {
                Resources = resources;
                ContinuationToken = continuationToken;
            }

            public IReadOnlyList<StagedPackageResource> Resources { get; }
            public string ContinuationToken { get; }
        }

        private sealed class EntryKeyProjection
        {
            public int Key { get; set; }
            public string PackageId { get; set; }
        }

        private sealed class EntryProjection
        {
            public int Key { get; set; }
            public int? GroupKey { get; set; }
            public string PackageId { get; set; }
            public bool HasPackageArtifact { get; set; }
            public StagingArtifactStatus? PackageArtifactStatus { get; set; }
            public bool HasSymbolArtifact { get; set; }
            public StagingArtifactStatus? SymbolArtifactStatus { get; set; }
            public PackageStatus ReferencePackageStatus { get; set; }
            public bool OwnerOwnsRegistration { get; set; }
        }
    }
}
