// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator;
using NuGetGallery;

namespace NuGet.Services.Validation.Symbols
{
    public class SymbolsMessageEnqueuer : ISymbolsMessageEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly TimeSpan? _messageDelay;
        private readonly IBrokeredMessageSerializer<SymbolsValidatorMessage> _serializer;
        private readonly IValidationStorageService _validationStorageService;
        private readonly IEntityService<StagedSymbolPackage> _stagedSymbolService;
        private readonly IStagingBlobService _stagingBlobService;

        public SymbolsMessageEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<SymbolsValidatorMessage> serializer,
            TimeSpan? messageDelay,
            IValidationStorageService validationStorageService,
            IEntityService<StagedSymbolPackage> stagedSymbolService,
            IStagingBlobService stagingBlobService)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageDelay = messageDelay;
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            _stagedSymbolService = stagedSymbolService ?? throw new ArgumentNullException(nameof(stagedSymbolService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        public async Task EnqueueSymbolsValidationMessageAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validationSet = await _validationStorageService.TryGetParentValidationSetAsync(request.ValidationId);
            if (validationSet == null || validationSet.PackageKey != request.PackageKey)
            {
                throw new InvalidOperationException($"Cannot find the matching validation set for {request.ValidationId}.");
            }

            string parentPackageUrl = null;
            if (validationSet.ValidatingType == ValidatingType.StagedSymbolPackage)
            {
                var attempt = _stagedSymbolService.FindPackageByKey(request.PackageKey)?.EntityRecord;
                if (attempt == null || attempt.Status != StagedPackageStatus.Validating || attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey != attempt.Key)
                {
                    throw new InvalidOperationException($"The staged symbol attempt is no longer eligible for validation {request.ValidationId}.");
                }

                var identity = attempt.StagedPackageIdentity;
                if (identity.Package.PackageStatusKey == PackageStatus.Staged && identity.CurrentStagedPackageKey.HasValue)
                {
                    var parent = identity.CurrentStagedPackage;
                    var uri = await _stagingBlobService.GetPackageReadUriAsync(parent.UploadedBlobPath, parent.UploadedBlobETag);
                    parentPackageUrl = uri.AbsoluteUri;
                }
                else if (identity.Package.PackageStatusKey != PackageStatus.Available)
                {
                    throw new InvalidOperationException($"The parent of staged symbol attempt {attempt.Key} is no longer available.");
                }
            }

            var message = new SymbolsValidatorMessage(
                validationId: request.ValidationId,
                symbolPackageKey: request.PackageKey,
                packageId: request.PackageId,
                packageNormalizedVersion: request.PackageVersion,
                snupkgUrl: request.NupkgUrl,
                parentPackageUrl: parentPackageUrl);
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_messageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}
