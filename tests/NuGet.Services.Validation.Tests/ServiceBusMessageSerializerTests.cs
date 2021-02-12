// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Validation.Tests
{
    public class ServiceBusMessageSerializerTests
    {
        private const string SchemaName = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";

        private const string StartValidationType = "StartValidation";
        private const string ProcessValidationSetType = "PackageValidationMessageData";
        private const string CheckValidationSetType = "CheckValidationSet";
        private const string CheckValidatorType = "PackageValidationCheckValidatorMessageData";

        private const int SchemaVersion1 = 1;
        private const int DeliveryCount = 2;

        private static readonly Guid ValidationTrackingId = new Guid("14b4c1b8-40e2-4d60-9db7-4b7195e807f5");
        private static readonly Guid ValidationId = new Guid("3fa83d31-3b44-4ffd-bfb8-02a9f5155af6");

        private const int PackageKey = 123;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3";
        private const string PackageNormalizedVersion = "4.3.0";

        private const string ContentType = "VsCodeExtensionV1";
        private static readonly Uri ContentUrl = new Uri("https://local.test/my/content");
        private static readonly JObject Properties = JObject.Parse(@"{ ""Foo"": ""Bar"" }");

        public class TheSerializePackageValidationMessageDataMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessageForStartValidation()
            {
                // Arrange
                var input = PackageValidationMessageData.NewStartValidation(
                    ValidationTrackingId,
                    ContentType,
                    ContentUrl,
                    Properties);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(StartValidationType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedStartValidationData, body);
            }

            [Fact]
            public void ProducesExpectedMessageForCheckValidator()
            {
                // Arrange
                var input = PackageValidationMessageData.NewCheckValidator(ValidationId);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(CheckValidatorType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedCheckValidatorData, body);
            }

            [Fact]
            public void ProducesExpectedMessageForPackageProcessValidationSet()
            {
                // Arrange
                var input = PackageValidationMessageData.NewProcessValidationSet(
                    PackageId,
                    PackageVersion,
                    ValidationTrackingId,
                    ValidatingType.Package,
                    PackageKey);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(ProcessValidationSetType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedProcessValidationSetDataPackage, body);
            }

            [Fact]
            public void ThrowsIfProcessValidationSetIsGivenGenericType()
            {
                // Arrange & Act
                var exception = Assert.Throws<ArgumentException>(
                    () => PackageValidationMessageData.NewProcessValidationSet(
                        PackageId,
                        PackageVersion,
                        ValidationTrackingId,
                        ValidatingType.Generic,
                        PackageKey));

                // Assert
                Assert.Contains("The validating type must be Package or SymbolPackage", exception.Message);
            }

            [Fact]
            public void ProducesExpectedMessageForCheckValidationSet()
            {
                // Arrange
                var input = PackageValidationMessageData.NewCheckValidationSet(
                    ValidationTrackingId,
                    extendExpiration: true);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(CheckValidationSetType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedCheckValidationSetData, body);
            }

            [Fact]
            public void ProducesExpectedMessageForSymbolsProcessValidationSet()
            {
                // Arrange
                var input = PackageValidationMessageData.NewProcessValidationSet(
                    PackageId,
                    PackageVersion,
                    ValidationTrackingId,
                    ValidatingType.SymbolPackage,
                    PackageKey);

                // Act
                var output = _target.SerializePackageValidationMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(ProcessValidationSetType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedProcessValidationSetDataSymbols, body);
            }
        }

        public class TheDeserializePackageValidationMessageDataMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessageForStartValidation()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessageForStartValidation();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(PackageValidationMessageType.StartValidation, output.Type);
                Assert.Equal(ValidationTrackingId, output.StartValidation.ValidationTrackingId);
                Assert.Equal(ContentType, output.StartValidation.ContentType);
                Assert.Equal(ContentUrl, output.StartValidation.ContentUrl);

                var property = Assert.Single(output.StartValidation.Properties.Properties());
                Assert.Equal("Foo", property.Name);
                Assert.Equal(JTokenType.String, property.Value.Type);
                Assert.Equal("Bar", property.Value.ToString());
            }

            [Fact]
            public void ProducesExpectedMessageForCheckValidator()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessageForCheckValidator();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageValidationMessageType.CheckValidator, output.Type);
                Assert.Equal(ValidationId, output.CheckValidator.ValidationId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
            }

            public static IEnumerable<object[]> SerializedProcessValidationSetData2 = new[]
            {
                new object[] { TestData.SerializedProcessValidationSetData2, PackageKey },
                new object[] { TestData.SerializedProcessValidationSetDataWithNoEntityKey2, null}
            };

            public static IEnumerable<object[]> SerializedProcessValidationSetDataForSymbols = new[]
            {
                new object[] { TestData.SerializedProcessValidationSetDataSymbols, PackageKey },
                new object[] { TestData.SerializedProcessValidationSetDataSymbolsWithNoEntityKey, null}
            };

            [Theory]
            [MemberData(nameof(SerializedProcessValidationSetData2))]
            public void ProducesExpectedMessageForProcessValidationSet(string serializedMessage, int? expectedKey)
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage(serializedMessage);

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageValidationMessageType.ProcessValidationSet, output.Type);
                Assert.Equal(PackageId, output.ProcessValidationSet.PackageId);
                Assert.Equal(PackageVersion, output.ProcessValidationSet.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.ProcessValidationSet.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ProcessValidationSet.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.Package, output.ProcessValidationSet.ValidatingType);
                Assert.Equal(expectedKey, output.ProcessValidationSet.EntityKey);
            }

            [Fact]
            public void ProducesExpectedMessageForPreviousVersionOfProcessValidationSet()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessagePrevious();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageValidationMessageType.ProcessValidationSet, output.Type);
                Assert.Equal(PackageId, output.ProcessValidationSet.PackageId);
                Assert.Equal(PackageVersion, output.ProcessValidationSet.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.ProcessValidationSet.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ProcessValidationSet.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.Package, output.ProcessValidationSet.ValidatingType);
                Assert.Null(output.ProcessValidationSet.EntityKey);
            }

            [Theory]
            [MemberData(nameof(SerializedProcessValidationSetDataForSymbols))]
            public void ProducesExpectedMessageForSymbolsProcessValidationSet(string serializedMessage, int? expectedKey)
            {
                // Arrange
                var brokeredMessage = GetBrokeredSymbolMessage(serializedMessage);

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(PackageValidationMessageType.ProcessValidationSet, output.Type);
                Assert.Equal(PackageId, output.ProcessValidationSet.PackageId);
                Assert.Equal(PackageVersion, output.ProcessValidationSet.PackageVersion);
                Assert.Equal(PackageNormalizedVersion, output.ProcessValidationSet.PackageNormalizedVersion);
                Assert.Equal(ValidationTrackingId, output.ProcessValidationSet.ValidationTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(ValidatingType.SymbolPackage, output.ProcessValidationSet.ValidatingType);
                Assert.Equal(expectedKey, output.ProcessValidationSet.EntityKey);
            }

            [Fact]
            public void ProducesExpectedMessageForCheckValidationSet()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessageForCheckValidationSet();

                // Act
                var output = _target.DeserializePackageValidationMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(DeliveryCount, output.DeliveryCount);
                Assert.Equal(PackageValidationMessageType.CheckValidationSet, output.Type);
                Assert.Equal(ValidationTrackingId, output.CheckValidationSet.ValidationTrackingId);
                Assert.True(output.CheckValidationSet.ExtendExpiration);
            }

            [Fact]
            public void RejectsInvalidSchemaName()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                var bad = "bad";
                brokeredMessage.Object.Properties[SchemaName] = bad;

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializePackageValidationMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided schema name '{bad}' is not supported.", exception.Message);
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
                    .Returns(expectedMessage ?? TestData.SerializedProcessValidationSetData2);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, ProcessValidationSetType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessagePrevious()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedProcessValidationSetData1);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, ProcessValidationSetType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessageForStartValidation()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedStartValidationData);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, StartValidationType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessageForCheckValidator()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedCheckValidatorData);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, CheckValidatorType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredSymbolMessage(string serializedMessage = null)
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(serializedMessage ?? TestData.SerializedProcessValidationSetDataSymbols);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, ProcessValidationSetType },
                        { SchemaVersionKey, SchemaVersion1 }
                    });
                return brokeredMessage;
            }

            private static Mock<IBrokeredMessage> GetBrokeredMessageForCheckValidationSet()
            {
                var brokeredMessage = new Mock<IBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedCheckValidationSetData);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, CheckValidationSetType },
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
