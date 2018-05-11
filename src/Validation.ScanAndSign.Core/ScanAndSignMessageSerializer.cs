// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignMessageSerializer : IBrokeredMessageSerializer<ScanAndSignMessage>
    {
        private const string SchemaName = "SignatureValidationMessageData";

        private IBrokeredMessageSerializer<ScanAndSignMessageData1> _serializer =
            new BrokeredMessageSerializer<ScanAndSignMessageData1>();

        public ScanAndSignMessage Deserialize(IBrokeredMessage message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            return new ScanAndSignMessage(
                deserializedMessage.OperationRequestType,
                deserializedMessage.PackageValidationId,
                deserializedMessage.BlobUri
                );
        }

        public IBrokeredMessage Serialize(ScanAndSignMessage message)
            => _serializer.Serialize(new ScanAndSignMessageData1
            {
                OperationRequestType = message.OperationRequestType,
                PackageValidationId = message.PackageValidationId,
                BlobUri = message.BlobUri
            });

        [Schema(Name = SchemaName, Version = 1)]
        private class ScanAndSignMessageData1
        {
            public OperationRequestType OperationRequestType { get; set; }
            public Guid PackageValidationId { get; set; }
            public Uri BlobUri { get; set; }
        }
    }
}
