// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using Xunit;

namespace Validation.Symbols.Core.Tests
{
    public class SymbolsValidatorMessageSerializerFacts
    {
        [Fact]
        public void RoundTripsV1WithParentSnapshotUrl()
        {
            var expected = new SymbolsValidatorMessage(
                Guid.NewGuid(),
                42,
                "Package",
                "1.0.0",
                "https://example/snupkg",
                "https://example/parent-nupkg");
            var target = new SymbolsValidatorMessageSerializer();
            var serialized = target.Serialize(expected);
            var received = new Mock<IReceivedBrokeredMessage>();
            received
                .Setup(x => x.Properties)
                .Returns(new Dictionary<string, object>(serialized.Properties));
            received.Setup(x => x.GetBody()).Returns(serialized.GetBody());

            var actual = target.Deserialize(received.Object);

            Assert.Equal(expected.ValidationId, actual.ValidationId);
            Assert.Equal(expected.SymbolsPackageKey, actual.SymbolsPackageKey);
            Assert.Equal(expected.SnupkgUrl, actual.SnupkgUrl);
            Assert.Equal(expected.ParentNupkgSnapshotUrl, actual.ParentNupkgSnapshotUrl);
            Assert.Equal("SymbolsValidatorMessageData", serialized.Properties[BrokeredMessageSerializer.SchemaNameKey]);
            Assert.Equal(1, serialized.Properties[BrokeredMessageSerializer.SchemaVersionKey]);
        }

        [Fact]
        public void DeserializesV1WithoutParentSnapshotUrl()
        {
            var validationId = Guid.NewGuid();
            var received = new Mock<IReceivedBrokeredMessage>();
            received
                .Setup(x => x.Properties)
                .Returns(new Dictionary<string, object>
                {
                    { BrokeredMessageSerializer.SchemaNameKey, "SymbolsValidatorMessageData" },
                    { BrokeredMessageSerializer.SchemaVersionKey, 1 },
                });
            received
                .Setup(x => x.GetBody())
                .Returns(
                    $"{{\"ValidationId\":\"{validationId}\",\"SymbolsPackageKey\":42," +
                    "\"PackageId\":\"Package\",\"PackageNormalizedVersion\":\"1.0.0\"," +
                    "\"SnupkgUrl\":\"https://example/snupkg\"}");

            var actual = new SymbolsValidatorMessageSerializer().Deserialize(received.Object);

            Assert.Equal(validationId, actual.ValidationId);
            Assert.Null(actual.ParentNupkgSnapshotUrl);
        }
    }
}
