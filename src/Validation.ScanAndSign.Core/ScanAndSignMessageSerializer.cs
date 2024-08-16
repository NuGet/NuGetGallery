// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignMessageSerializer : IBrokeredMessageSerializer<ScanAndSignMessage>
    {
        private const string SchemaName = "SignatureValidationMessageData";

        private IBrokeredMessageSerializer<ScanAndSignMessageData1> _serializer1 =
            new BrokeredMessageSerializer<ScanAndSignMessageData1>();
        private IBrokeredMessageSerializer<ScanAndSignMessageData2> _serializer2 =
            new BrokeredMessageSerializer<ScanAndSignMessageData2>();

        public ScanAndSignMessage Deserialize(IReceivedBrokeredMessage message)
        {
            try
            {
                var deserializedMessage2 = _serializer2.Deserialize(message);

                return new ScanAndSignMessage(
                    deserializedMessage2.OperationRequestType,
                    deserializedMessage2.PackageValidationId,
                    deserializedMessage2.BlobUri,
                    deserializedMessage2.V3ServiceIndexUrl,
                    deserializedMessage2.Owners,
                    deserializedMessage2.Context);
            }
            catch (FormatException)
            {
                // do nothing, will try to deserialize with v1 below
            }

            var deserializedMessage1 = _serializer1.Deserialize(message);

            return new ScanAndSignMessage(
                deserializedMessage1.OperationRequestType,
                deserializedMessage1.PackageValidationId,
                deserializedMessage1.BlobUri,
                deserializedMessage1.V3ServiceIndexUrl,
                deserializedMessage1.Owners,
                new Dictionary<string, string>());
        }

        public IBrokeredMessage Serialize(ScanAndSignMessage message)
            => _serializer2.Serialize(new ScanAndSignMessageData2
            {
                OperationRequestType = message.OperationRequestType,
                PackageValidationId = message.PackageValidationId,
                BlobUri = message.BlobUri,
                V3ServiceIndexUrl = message.V3ServiceIndexUrl,
                Owners = message.Owners,
                Context = message.Context,
            });

        [Schema(Name = SchemaName, Version = 1)]
        private class ScanAndSignMessageData1
        {
            public OperationRequestType OperationRequestType { get; set; }
            public Guid PackageValidationId { get; set; }
            public Uri BlobUri { get; set; }
            public string V3ServiceIndexUrl { get; set; }
            public IReadOnlyList<string> Owners { get; set; }
        }

        [Schema(Name = SchemaName, Version = 2)]
        private class ScanAndSignMessageData2
        {
            public OperationRequestType OperationRequestType { get; set; }
            public Guid PackageValidationId { get; set; }
            public Uri BlobUri { get; set; }
            public string V3ServiceIndexUrl { get; set; }
            public IReadOnlyList<string> Owners { get; set; }
            public IReadOnlyDictionary<string, string> Context { get; set; }
        }
    }
}
