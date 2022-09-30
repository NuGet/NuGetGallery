// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignEnqueuer : IScanAndSignEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<ScanAndSignMessage> _serializer;
        private readonly ScanAndSignEnqueuerConfiguration _configuration;
        private readonly ILogger<IScanAndSignEnqueuer> _logger;

        public ScanAndSignEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<ScanAndSignMessage> serializer,
            IOptionsSnapshot<ScanAndSignEnqueuerConfiguration> configurationAccessor,
            ILogger<IScanAndSignEnqueuer> logger)
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

        public Task EnqueueScanAsync(
            Guid validationId,
            string nupkgUrl,
            IReadOnlyDictionary<string, string> context)
            => EnqueueScanImplAsync(validationId, nupkgUrl, context, messageDeliveryDelayOverride: null);

        public Task EnqueueScanAsync(
            Guid validationId,
            string nupkgUrl,
            IReadOnlyDictionary<string, string> context,
            TimeSpan messageDeliveryDelayOverride)
            => EnqueueScanImplAsync(validationId, nupkgUrl, context, messageDeliveryDelayOverride);

        public Task EnqueueScanAndSignAsync(
            Guid validationId,
            string nupkgUrl,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context)
            => EnqueueScanAndSignImplAsync(validationId, nupkgUrl, v3ServiceIndexUrl, owners, context, messageDeliveryDelayOverride: null);

        public Task EnqueueScanAndSignAsync(
            Guid validationId,
            string nupkgUrl,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context,
            TimeSpan messageDeliveryDelayOverride)
            => EnqueueScanAndSignImplAsync(validationId, nupkgUrl, v3ServiceIndexUrl, owners, context, messageDeliveryDelayOverride);

        private Task EnqueueScanImplAsync(
            Guid validationId,
            string nupkgUrl,
            IReadOnlyDictionary<string, string> context,
            TimeSpan? messageDeliveryDelayOverride = null)
        {
            if (nupkgUrl == null)
            {
                throw new ArgumentNullException(nameof(nupkgUrl));
            }

            if (messageDeliveryDelayOverride.HasValue && messageDeliveryDelayOverride < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(messageDeliveryDelayOverride), $"{nameof(messageDeliveryDelayOverride)} cannot be negative");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _logger.LogInformation(
                "Enqueuing scan only message for validation {ValidationId} {BlobUrl}, delay override: {DelayOverride}",
                validationId,
                nupkgUrl,
                messageDeliveryDelayOverride);

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Scan,
                    validationId,
                    new Uri(nupkgUrl),
                    context),
                messageDeliveryDelayOverride);
        }

        private Task EnqueueScanAndSignImplAsync(
            Guid validationId,
            string nupkgUrl,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context,
            TimeSpan? messageDeliveryDelayOverride = null)
        {
            if (nupkgUrl == null)
            {
                throw new ArgumentNullException(nameof(nupkgUrl));
            }

            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(v3ServiceIndexUrl))
            {
                throw new ArgumentException("The service index URL parameter is required", nameof(v3ServiceIndexUrl));
            }

            if (messageDeliveryDelayOverride.HasValue && messageDeliveryDelayOverride < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(messageDeliveryDelayOverride), $"{nameof(messageDeliveryDelayOverride)} cannot be negative");
            }

            _logger.LogInformation(
                "Enqueuing scan and sign message for validation {ValidationId} {BlobUrl} using service index {ServiceIndex} and owners {Owners}, delay override: {DelayOverride}",
                validationId,
                nupkgUrl,
                v3ServiceIndexUrl,
                owners,
                messageDeliveryDelayOverride);

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Sign,
                    validationId,
                    new Uri(nupkgUrl),
                    v3ServiceIndexUrl,
                    owners,
                    context),
                messageDeliveryDelayOverride);
        }

        private Task SendScanAndSignMessageAsync(ScanAndSignMessage message, TimeSpan? messageDeliveryDelayOverride)
        {
            var brokeredMessage = _serializer.Serialize(message);

            var delay = messageDeliveryDelayOverride ?? _configuration.MessageDelay ?? TimeSpan.Zero;

            var visibleAt = DateTimeOffset.UtcNow + delay;
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
