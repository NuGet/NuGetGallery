// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

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

        public Task EnqueueScanAsync(Guid validationId, string nupkgUrl)
        {
            if (nupkgUrl == null)
            {
                throw new ArgumentNullException(nameof(nupkgUrl));
            }

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Scan,
                    validationId,
                    new Uri(nupkgUrl)));
        }

        public Task EnqueueScanAndSignAsync(Guid validationId, string nupkgUrl, string v3ServiceIndexUrl, IReadOnlyList<string> owners)
        {
            if (nupkgUrl == null)
            {
                throw new ArgumentNullException(nameof(nupkgUrl));
            }

            if (owners == null) throw new ArgumentNullException(nameof(owners));

            if (string.IsNullOrEmpty(v3ServiceIndexUrl))
            {
                throw new ArgumentException("The service index URL parameter is required", nameof(v3ServiceIndexUrl));
            }

            _logger.LogInformation(
                "Requested scan and sign for validation {ValidationId} {BlobUrl} using service index {ServiceIndex} and owners {Owners}",
                validationId,
                nupkgUrl,
                v3ServiceIndexUrl,
                owners);

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Sign,
                    validationId,
                    new Uri(nupkgUrl),
                    v3ServiceIndexUrl,
                    owners));
        }

        private Task SendScanAndSignMessageAsync(ScanAndSignMessage message)
        {
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
