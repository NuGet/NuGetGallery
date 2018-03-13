// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.PackageCertificates
{
    public class CertificateVerificationEnqueuer : ICertificateVerificationEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IOptionsSnapshot<PackageCertificatesConfiguration> _configuration;
        private readonly IBrokeredMessageSerializer<CertificateValidationMessage> _serializer;

        public CertificateVerificationEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<CertificateValidationMessage> serializer,
            IOptionsSnapshot<PackageCertificatesConfiguration> configuration)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task EnqueueVerificationAsync(IValidationRequest request, EndCertificate certificate)
        {
            var message = new CertificateValidationMessage(certificate.Key, request.ValidationId);
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.Value.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}