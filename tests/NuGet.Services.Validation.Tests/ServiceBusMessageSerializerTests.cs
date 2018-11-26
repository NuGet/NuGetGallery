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
        private const string SchemaName = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3";
        private const string PackageNormalizedVersion = "4.3.0";
        private static readonly Guid ValidationTrackingId = new Guid("14b4c1b8-40e2-4d60-9db7-4b7195e807f5");
        private const string PackageValidationMessageDataType = "PackageValidationMessageData";
        private const int SchemaVersion1 = 1;
        private const int DeliveryCount = 2;
        private const int PackageKey = 123;

        public class TheSerializePackageValidationMessageDataMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var input = new PackageValidationMessageData(PackageId, PackageVersion, ValidationTrackingId, ValidatingType.Package, PackageKey);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(PackageValidationMessageDataType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedPackageValidationMessageDataPackage, body);
            }

            [Fact]
            public void ProducesExpectedMessageForSymbols()
            {
                // Arrange
                var input = new PackageValidationMessageData(PackageId, PackageVersion, ValidationTrackingId, ValidatingType.SymbolPackage, PackageKey);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(PackageValidationMessageDataType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedPackageValidationMessageDataSymbols, body);
            }
        }

        public class TheDeserializePackageValidationMessageDataMethod : Base
        {
            private const string TypeValue = "PackageValidationMessageData";

            public static IEnumerable<object[]> SerializedTestMessageData2 = new[]
            {
                new object[] { TestData.SerializedPackageValidationMessageData2, PackageKey },
                new object[] { TestData.SerializedPackageValidationMessageDataWithNoEntityKey2, null}
            };

            public static IEnumerable<object[]> SerializedTestMessageDataForSymbols = new[]
            {
                new object[] { TestData.SerializedPackageValidationMessageDataSymbols, PackageKey },
                new object[] { TestData.SerializedPackageValidationMessageDataSymbolsWithNoEntityKey, null}
            };

            [Theory]
            [MemberData(nameof(SerializedTestMessageData2))]
            public void ProducesExpectedMessage(string serializedMessage, int? expectedKey)
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage(serializedMessage);

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageId, output.PackageId);
                Assert.Equal(PackageVersion, output.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.Package, output.ValidatingType);
                Assert.Equal(expectedKey, output.EntityKey);
            }

            [Fact]
            public void ProducesExpectedMessageForPreviousVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessagePrevious();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageId, output.PackageId);
                Assert.Equal(PackageVersion, output.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.Package, output.ValidatingType);
                Assert.Null(output.EntityKey);
            }

            [Theory]
            [MemberData(nameof(SerializedTestMessageDataForSymbols))]
            public void ProducesExpectedMessageForSymbols(string serializedMessage, int? expectedKey)
            {
                // Arrange
                var brokeredMessage = GetBrokeredSymbolMessage(serializedMessage);

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageId, output.PackageId);
                Assert.Equal(PackageVersion, output.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.SymbolPackage, output.ValidatingType);
                Assert.Equal(expectedKey, output.EntityKey);
            }

            [Fact]
            public void RejectsInvalidType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Object.Properties[SchemaName] = "bad";

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaName} property '{PackageValidationMessageDataType}'.", exception.Message);
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
                brokeredMessage.Object.Properties.Remove(SchemaName);

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaName} property.", exception.Message);
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
                brokeredMessage.Object.Properties[SchemaName] = -1;

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaName} property that is not a string.", exception.Message);
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

            private static Mock<IBrokeredMessage> GetBrokeredMessage(string expectedMessage = null)
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(expectedMessage ?? TestData.SerializedPackageValidationMessageData2);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, PackageValidationMessageDataType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessagePrevious()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedPackageValidationMessageData1);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, PackageValidationMessageDataType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredSymbolMessage(string serializedMessage = null)
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(serializedMessage ?? TestData.SerializedPackageValidationMessageDataSymbols);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, PackageValidationMessageDataType },
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
