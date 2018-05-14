// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
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

        public ScanAndSignEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<ScanAndSignMessage> serializer,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

            var message = new ScanAndSignMessage(
                OperationRequestType.Scan,
                request.ValidationId,
                new Uri(request.NupkgUrl));
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
