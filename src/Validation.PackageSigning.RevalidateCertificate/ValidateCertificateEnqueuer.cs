// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class ValidateCertificateEnqueuer : IValidateCertificateEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<CertificateValidationMessage> _serializer;

        public ValidateCertificateEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<CertificateValidationMessage> serializer)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public async Task EnqueueValidationAsync(Guid validationId, EndCertificate certificate)
        {
            var message = new CertificateValidationMessage(
                certificate.Key,
                validationId,
                revalidateRevokedCertificate: false,
                sendCheckValidator: false);

            var brokeredMessage = _serializer.Serialize(message);

            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}