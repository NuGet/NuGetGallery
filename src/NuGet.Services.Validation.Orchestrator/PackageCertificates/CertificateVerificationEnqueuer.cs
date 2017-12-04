// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.PackageCertificates
{
    public class CertificateVerificationEnqueuer : ICertificateVerificationEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private static readonly IBrokeredMessageSerializer<CertificateValidationMessage> _serializer = new CertificateValidationMessageSerializer();

        public CertificateVerificationEnqueuer(ITopicClient topicClient)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
        }

        public async Task EnqueueVerificationAsync(IValidationRequest request, EndCertificate certificate)
        {
            var message = new CertificateValidationMessage(certificate.Key, request.ValidationId);
            var brokeredMessage = _serializer.Serialize(message);

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}