// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageTerminalStateProcessor : IStagedPackageTerminalStateProcessor
    {
        private static readonly TimeSpan ValidationSetReadDuration = TimeSpan.FromMinutes(10);

        private readonly IEntitiesContext _entitiesContext;
        private readonly IValidationFileService _packageFileService;
        private readonly IFileDownloader _fileDownloader;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly IValidatorProvider _validatorProvider;
        private readonly ICorePackageService _corePackageService;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ILogger<StagedPackageTerminalStateProcessor> _logger;

        public StagedPackageTerminalStateProcessor(
            IEntitiesContext entitiesContext,
            IValidationFileService packageFileService,
            IFileDownloader fileDownloader,
            IStagingBlobService stagingBlobService,
            IValidatorProvider validatorProvider,
            ICorePackageService corePackageService,
            IPackageValidationEnqueuer validationEnqueuer,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            ILogger<StagedPackageTerminalStateProcessor> logger)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _corePackageService = corePackageService ?? throw new ArgumentNullException(nameof(corePackageService));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _validationConfiguration = validationConfigurationAccessor?.Value ?? throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessAsync(PackageValidationSet validationSet, Package package, StagingArtifactStatus status)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (status != StagingArtifactStatus.Ready && status != StagingArtifactStatus.ValidationFailed)
            {
                throw new ArgumentException(
                    $"A staged package validation can only transition to {nameof(StagingArtifactStatus.Ready)} or " +
                    $"{nameof(StagingArtifactStatus.ValidationFailed)}.",
                    nameof(status));
            }

            var artifact = _entitiesContext.StagedPackageArtifacts.SingleOrDefault(a =>
                a.StagingEntry.PackageKey == package.Key
                && a.ValidationTrackingId == validationSet.ValidationTrackingId
                && a.ContentHash == package.Hash
                && a.Status == StagingArtifactStatus.Validating);

            if (artifact == null)
            {
                _logger.LogInformation(
                    "Ignoring stale staged package outcome for package key {PackageKey}, validation set {ValidationTrackingId}.",
                    package.Key,
                    validationSet.ValidationTrackingId);
                return;
            }

            if (status == StagingArtifactStatus.ValidationFailed)
            {
                artifact.Status = StagingArtifactStatus.ValidationFailed;
                artifact.ValidatedDate = null;
                await _entitiesContext.SaveChangesAsync();
                return;
            }

            var oldBlobPath = artifact.BlobPath;
            var oldBlobETag = artifact.BlobETag;
            var oldContentHash = artifact.ContentHash;
            var hasProcessor = validationSet.PackageValidations.Any(v => _validatorProvider.IsNuGetProcessor(v.Type));

            if (hasProcessor)
            {
                var packageUri = await _packageFileService.GetPackageForValidationSetReadUriAsync(
                    validationSet,
                    sasDefinition: null,
                    endOfAccess: DateTimeOffset.UtcNow.Add(ValidationSetReadDuration));

                StagingBlobReference replacement;
                using (var download = await _fileDownloader.DownloadAsync(packageUri, CancellationToken.None))
                {
                    replacement = await _stagingBlobService.CreateAsync(
                        download.GetStreamOrThrow(),
                        StagingBlobType.Nupkg);
                }

                artifact.BlobPath = replacement.BlobPath;
                artifact.BlobETag = replacement.ETag;
                artifact.ContentHash = replacement.ContentHash;

                await _corePackageService.UpdatePackageStreamMetadataAsync(
                    package,
                    new PackageStreamMetadata
                    {
                        Hash = replacement.ContentHash,
                        HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                        Size = replacement.ContentLength,
                    },
                    commitChanges: false);

                _entitiesContext.StagingBlobCleanups.Add(new StagingBlobCleanup
                {
                    BlobPath = oldBlobPath,
                    ExpectedETag = oldBlobETag,
                    CreatedDate = DateTime.UtcNow,
                });

                if (!string.Equals(oldContentHash, replacement.ContentHash, StringComparison.Ordinal))
                {
                    await RevalidateRetainedSymbolAsync(validationSet, artifact, replacement.ContentHash);
                }
            }

            artifact.Status = StagingArtifactStatus.Ready;
            artifact.ValidatedDate = DateTime.UtcNow;
            await _entitiesContext.SaveChangesAsync();
        }

        private async Task RevalidateRetainedSymbolAsync(PackageValidationSet validationSet, StagedPackageArtifact packageArtifact, string parentContentHash)
        {
            var symbolArtifact = _entitiesContext.StagedSymbolArtifacts.SingleOrDefault(a => a.StagingEntryKey == packageArtifact.StagingEntryKey);
            if (symbolArtifact == null)
            {
                return;
            }

            if (symbolArtifact.Status == StagingArtifactStatus.Promoting || symbolArtifact.Status == StagingArtifactStatus.PromotionFailed)
            {
                throw new InvalidOperationException("A retained staged symbol artifact cannot be revalidated while promotion is active.");
            }

            var trackingId = Guid.NewGuid();
            symbolArtifact.Status = StagingArtifactStatus.Validating;
            symbolArtifact.ValidationTrackingId = trackingId;
            symbolArtifact.ParentContentHash = parentContentHash;
            symbolArtifact.ValidatedDate = null;

            await _validationEnqueuer.SendMessageAsync(
                PackageValidationMessageData.NewProcessValidationSet(
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    trackingId,
                    ValidatingType.SymbolPackage,
                    symbolArtifact.SymbolPackageKey),
                DateTimeOffset.UtcNow + _validationConfiguration.ValidationMessageRecheckPeriod);
        }
    }
}
