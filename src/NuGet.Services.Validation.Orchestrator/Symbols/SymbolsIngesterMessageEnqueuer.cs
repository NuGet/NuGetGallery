// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Symbols
{
    public class SymbolsIngesterMessageEnqueuer : ISymbolsIngesterMessageEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly TimeSpan? _messageDelay;
        private readonly IBrokeredMessageSerializer<SymbolsIngesterMessage> _serializer;

        public SymbolsIngesterMessageEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<SymbolsIngesterMessage> serializer,
            TimeSpan? messageDelay)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageDelay = messageDelay;
        }

        public async Task<SymbolsIngesterMessage> EnqueueSymbolsIngestionMessageAsync(INuGetValidationRequest request)
        {
            var message = new SymbolsIngesterMessage(validationId: request.ValidationId, 
                symbolPackageKey: request.PackageKey,
                packageId: request.PackageId,
                packageNormalizedVersion: request.PackageVersion,
                snupkgUrl: request.NupkgUrl,
                requestName : SymbolsValidationEntitiesService.CreateSymbolServerRequestNameFromValidationRequest(request));
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_messageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            await _topicClient.SendAsync(brokeredMessage);
            return message;
        }
    }
}
