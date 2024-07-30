// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.PackageSigning.Messages
{
    public class SignatureValidationMessageSerializer
        : IBrokeredMessageSerializer<SignatureValidationMessage>
    {
        private const string SignatureValidationSchemaName = "SignatureValidationMessageData";

        private IBrokeredMessageSerializer<SignatureValidationMessageData1> _serializer =
            new BrokeredMessageSerializer<SignatureValidationMessageData1>();

        public IBrokeredMessage Serialize(SignatureValidationMessage message)
        {
            return _serializer.Serialize(new SignatureValidationMessageData1
            {
                PackageId = message.PackageId,
                PackageVersion = message.PackageVersion,
                NupkgUri = message.NupkgUri,
                ValidationId = message.ValidationId,
                RequireReopsitorySignature = message.RequireRepositorySignature,
            });
        }

        public SignatureValidationMessage Deserialize(IReceivedBrokeredMessage message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            return new SignatureValidationMessage(
                deserializedMessage.PackageId,
                deserializedMessage.PackageVersion,
                deserializedMessage.NupkgUri,
                deserializedMessage.ValidationId,
                deserializedMessage.RequireReopsitorySignature);
        }

        [Schema(Name = SignatureValidationSchemaName, Version = 1)]
        private class SignatureValidationMessageData1
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public Uri NupkgUri { get; set; }
            public Guid ValidationId { get; set; }
            public bool RequireReopsitorySignature { get; set; }
        }
    }
}