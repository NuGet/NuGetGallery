// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Validation.Tests
{
    public class ServiceBusMessageSerializerTests
    {
        private const string SchemaVersionKey = "SchemaVersion";
        private const string TypeKey = "Type";
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0";
        private static readonly Guid ValidationTrackingId = new Guid("14b4c1b8-40e2-4d60-9db7-4b7195e807f5");
        private const string PackageValidationMessageDataType = "PackageValidationMessageData";
        private const int SchemaVersion1 = 1;

        public class TheSerializePackageValidationMessageDataMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var input = new PackageValidationMessageData(PackageId, PackageVersion, ValidationTrackingId);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(TypeKey, output.Properties.Keys);
                Assert.Equal(PackageValidationMessageDataType, output.Properties[TypeKey]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedPackageValidationMessageData1, body);
            }
        }

        public class TheDeserializePackageValidationMessageDataMethod : Base
        {
            private const string TypeValue = "PackageValidationMessageData";

            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageId, output.PackageId);
                Assert.Equal(PackageVersion, output.PackageVersion);
                Assert.Equal(ValidationTrackingId, output.ValidationTrackingId);
            }

            [Fact]
            public void RejectsInvalidType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties[TypeKey] = "bad";

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {TypeKey} property '{PackageValidationMessageDataType}'.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties[SchemaVersionKey] = -1;

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaVersionKey} property '1'.", exception.Message);
            }

            [Fact]
            public void RejectsMissingType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties.Remove(TypeKey);

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {TypeKey} property.", exception.Message);
            }

            [Fact]
            public void RejectsMissingSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties.Remove(SchemaVersionKey);

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaVersionKey} property.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidTypeType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties[TypeKey] = -1;

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {TypeKey} property that is not a string.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersionType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties[SchemaVersionKey] = "bad";

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaVersionKey} property that is not an integer.", exception.Message);
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessage()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedPackageValidationMessageData1);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { TypeKey, PackageValidationMessageDataType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }
        }

        public abstract class Base
        {
            protected readonly ServiceBusMessageSerializer _target;

            public Base()
            {
                _target = new ServiceBusMessageSerializer();
            }
        }
    }
}
