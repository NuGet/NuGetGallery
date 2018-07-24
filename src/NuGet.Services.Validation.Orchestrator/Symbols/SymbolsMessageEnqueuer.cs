// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Symbols
{
    public class SymbolsMessageEnqueuer : ISymbolsMessageEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IOptionsSnapshot<SymbolsValidationConfiguration> _configuration;
        private readonly IBrokeredMessageSerializer<SymbolsValidatorMessage> _serializer;

        public SymbolsMessageEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<SymbolsValidatorMessage> serializer,
            IOptionsSnapshot<SymbolsValidationConfiguration> configuration)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task EnqueueSymbolsValidationMessageAsync(IValidationRequest request)
        {
            var message = new SymbolsValidatorMessage(validationId: request.ValidationId, 
                symbolPackageKey: request.PackageKey,
                packageId: request.PackageId,
                packageNormalizedVersion: request.PackageVersion,
                snupkgUrl: request.NupkgUrl);
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.Value.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}
