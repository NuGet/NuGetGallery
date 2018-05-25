// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public class ScanAndSignEnqueuer : IScanAndSignEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<ScanAndSignMessage> _serializer;
        private readonly ScanAndSignConfiguration _configuration;
        private readonly ILogger<IScanAndSignEnqueuer> _logger;

        public ScanAndSignEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<ScanAndSignMessage> serializer,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor,
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

        public Task EnqueueScanAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Scan,
                    request.ValidationId,
                    new Uri(request.NupkgUrl)));
        }

        public Task EnqueueScanAndSignAsync(IValidationRequest request, string v3ServiceIndexUrl, List<string> owners)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (owners == null) throw new ArgumentNullException(nameof(owners));

            if (string.IsNullOrEmpty(v3ServiceIndexUrl))
            {
                throw new ArgumentException("The service index URL parameter is required", nameof(v3ServiceIndexUrl));
            }

            _logger.LogInformation(
                "Requested scan and sign for package {PackageId} {PackageVersion} using service index {ServiceIndex} and owners {Owners}",
                request.PackageId,
                request.PackageVersion,
                v3ServiceIndexUrl,
                owners);

            return SendScanAndSignMessageAsync(
                new ScanAndSignMessage(
                    OperationRequestType.Sign,
                    request.ValidationId,
                    new Uri(request.NupkgUrl),
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
