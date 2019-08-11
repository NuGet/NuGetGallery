// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.PackageSigning.Messages
{
    public class CertificateValidationMessageSerializer : IBrokeredMessageSerializer<CertificateValidationMessage>
    {
        private const string CertificateValidationSchemaName = "CertificateValidationMessageData";

        private IBrokeredMessageSerializer<CertificateValidationMessageData1> _serializer =
            new BrokeredMessageSerializer<CertificateValidationMessageData1>();

        public IBrokeredMessage Serialize(CertificateValidationMessage message)
        {
            return _serializer.Serialize(new CertificateValidationMessageData1
            {
                CertificateKey = message.CertificateKey,
                ValidationId = message.ValidationId,
                RevalidateRevokedCertificate = message.RevalidateRevokedCertificate,
                SendCheckValidator = message.SendCheckValidator,
            });
        }

        public CertificateValidationMessage Deserialize(IBrokeredMessage brokeredMessage)
        {
            var message = _serializer.Deserialize(brokeredMessage);

            return new CertificateValidationMessage(
                message.CertificateKey,
                message.ValidationId,
                message.RevalidateRevokedCertificate,
                message.SendCheckValidator);
        }

        [Schema(Name = CertificateValidationSchemaName, Version = 1)]
        private struct CertificateValidationMessageData1
        {
            public long CertificateKey { get; set; }
            public Guid ValidationId { get; set; }
            public bool RevalidateRevokedCertificate { get; set; }
            public bool SendCheckValidator { get; set; }
        }
    }
}
