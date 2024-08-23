// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Newtonsoft.Json;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.ServiceBus;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class ScanAndSignMessageSerializerFacts
    {
        [Fact]
        public void DeserializesVersion1()
        {
            var validationId = Guid.NewGuid();
            var packageUrl = new Uri("https://package");
            const string v3ServiceIndexUrl = "foobar";
            var owners = new List<string> { "owner1", "owner2" };
            var messageV1 = CreateMessage(1, OperationRequestType.Sign, validationId, packageUrl, v3ServiceIndexUrl, owners, context: null);

            var result1 = _target.Deserialize(messageV1);

            Assert.Equal(OperationRequestType.Sign, result1.OperationRequestType);
            Assert.Equal(validationId, result1.PackageValidationId);
            Assert.Equal(packageUrl, result1.BlobUri);
            Assert.Equal(v3ServiceIndexUrl, result1.V3ServiceIndexUrl);
            Assert.Equal(owners, result1.Owners);
        }

        [Fact]
        public void DeserializesVersion2()
        {
            var validationId = Guid.NewGuid();
            var packageUrl = new Uri("https://package");
            const string v3ServiceIndexUrl = "foobar";
            var owners = new List<string> { "owner1", "owner2" };
            var context = new Dictionary<string, string> { { "property1", "value1" }, { "property2", "value2" } };
            var messageV2 = CreateMessage(2, OperationRequestType.Sign, validationId, packageUrl, v3ServiceIndexUrl, owners, context);

            var result2 = _target.Deserialize(messageV2);

            Assert.Equal(OperationRequestType.Sign, result2.OperationRequestType);
            Assert.Equal(validationId, result2.PackageValidationId);
            Assert.Equal(packageUrl, result2.BlobUri);
            Assert.Equal(v3ServiceIndexUrl, result2.V3ServiceIndexUrl);
            Assert.Equal(owners, result2.Owners);
            Assert.Equal(context, result2.Context);
        }

        [Fact]
        public void SerializesVersion2()
        {
            var message = new ScanAndSignMessage(
                OperationRequestType.Sign,
                Guid.NewGuid(),
                new Uri("https://package"),
                "serviceIndexUrl",
                new List<string>(),
                new Dictionary<string, string>());

            var brokeredMessage = _target.Serialize(message);

            Assert.Equal(2, brokeredMessage.Properties["SchemaVersion"]);
        }

        private ScanAndSignMessageSerializer _target = new ScanAndSignMessageSerializer();

        private IReceivedBrokeredMessage CreateMessage(
            int version,
            OperationRequestType operationType,
            Guid validationId,
            Uri blobUri,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context)
        {
            var payload = new Dictionary<string, object>
            {
                { "operationRequestType", operationType.ToString() },
                { "packageValidationId", validationId },
                { "blobUri", blobUri },
                { "v3ServiceIndexUrl", v3ServiceIndexUrl },
                { "owners", owners },
            };
            if (version == 2 && context != null)
            {
                payload["context"] = context;
            }

            var payloadStr = JsonConvert.SerializeObject(payload);

            var message = new Mock<IReceivedBrokeredMessage>();
            message.Setup(x => x.Properties).Returns(new Dictionary<string, object>
            {
                { "SchemaName", "SignatureValidationMessageData" },
                { "SchemaVersion", version },
            });

            message.Setup(x => x.GetBody()).Returns(payloadStr);

            return message.Object;
        }
    }
}
