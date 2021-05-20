// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class ContentScanEnqueuer : IContentScanEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<ContentScanData> _serializer;
        private readonly ContentScanEnqueuerConfiguration _configuration;
        private readonly ILogger<IContentScanEnqueuer> _logger;

        public ContentScanEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<ContentScanData> serializer,
            IOptionsSnapshot<ContentScanEnqueuerConfiguration> configurationAccessor,
            ILogger<IContentScanEnqueuer> logger)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value;
        }

        public Task EnqueueContentScanAsync(Guid validationStepId, Uri inputUrl)
            => EnqueueScanImplAsync(validationStepId, inputUrl, messageDeliveryDelayOverride: null);

        public Task EnqueueContentScanAsync(Guid validationStepId, Uri inputUrl, TimeSpan messageDeliveryDelayOverride)
            => EnqueueScanImplAsync(validationStepId, inputUrl, messageDeliveryDelayOverride);

        private async Task EnqueueScanImplAsync(Guid validationStepId, Uri inputUrl, TimeSpan? messageDeliveryDelayOverride = null)
        {
            if (inputUrl == null)
            {
                throw new ArgumentNullException(nameof(inputUrl));
            }

            if (messageDeliveryDelayOverride.HasValue && messageDeliveryDelayOverride < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(messageDeliveryDelayOverride), $"{nameof(messageDeliveryDelayOverride)} cannot be negative");
            }

            _logger.LogInformation(
                "Requested scan only for validation {ValidationStepId} {BlobUrl}, delay override: {DelayOverride}",
                validationStepId,
                new UriBuilder(inputUrl) { Query = "REDACTED" }.Uri.AbsoluteUri,
                messageDeliveryDelayOverride);

            await SendContentScanMessageAsync(
                ContentScanData.NewStartContentScanData(
                    validationStepId,
                    inputUrl),
                messageDeliveryDelayOverride);
        }

        private async Task SendContentScanMessageAsync(ContentScanData message, TimeSpan? messageDeliveryDelayOverride)
        {
            var brokeredMessage = _serializer.Serialize(message);

            var delay = messageDeliveryDelayOverride ?? _configuration.MessageDelay ?? TimeSpan.Zero;

            var visibleAt = DateTimeOffset.UtcNow + delay;
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}
