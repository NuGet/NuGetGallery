// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGet.Services.ServiceBus.Tests
{
    public class BrokeredMessageSerializerFacts
    {
        private const string SchemaNameKey = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";

        private const string SchemaName = "Foo";
        private const int SchemaVersion23 = 23;

        private const string JsonSerializedContent = "{\"A\":\"Hello World\"}";

        [Schema(Name = "Foo", Version = 23)]
        public class SchematizedType
        {
            public string A { get; set; }
        }

        public class UnSchematizedType { }

        public class TheConstructor : Base
        {
            [Fact]
            public void ThrowsIfSchemaDoesntHaveSchemaVersionAttribute()
            {
                Action runConstructor = () => new BrokeredMessageSerializer<UnSchematizedType>();
                var exception = Assert.Throws<TypeInitializationException>(runConstructor);

                Assert.Equal(typeof(InvalidOperationException), exception.InnerException.GetType());
                Assert.Contains($"{nameof(UnSchematizedType)} must have exactly one {nameof(SchemaAttribute)}", exception.InnerException.Message);
            }
        }

        public class TheSerializeMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var input = new SchematizedType { A = "Hello World" };

                // Act
                var output = _target.Serialize(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion23, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaNameKey, output.Properties.Keys);
                Assert.Equal(SchemaName, output.Properties[SchemaNameKey]);
                var body = output.GetBody();
                Assert.Equal(JsonSerializedContent, body);
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
                var output = _target.Deserialize(brokeredMessage.Object);

                // Assert
                Assert.Equal("Hello World", output.A);
            }

            [Fact]
            public void RejectsInvalidType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, "bad" },
                    { SchemaVersionKey, SchemaVersion23 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaNameKey} property '{SchemaName}'.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, -1 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaVersionKey} property '23'.", exception.Message);
            }

            [Fact]
            public void RejectsMissingType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaVersionKey, SchemaVersion23 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaNameKey} property.", exception.Message);
            }

            [Fact]
            public void RejectsMissingSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaVersionKey} property.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidTypeType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, -1 },
                    { SchemaVersionKey, SchemaVersion23 }
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaNameKey} property that is not a string.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersionType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, "bad" }
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.Deserialize(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaVersionKey} property that is not an integer.", exception.Message);
            }

            private static Mock<IReceivedBrokeredMessage> GetBrokeredMessage()
            {
                var brokeredMessage = new Mock<IReceivedBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(JsonSerializedContent);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaNameKey, SchemaName },
                        { SchemaVersionKey, SchemaVersion23 }
                    });
                return brokeredMessage;
            }
        }

        public abstract class Base
        {
            protected readonly BrokeredMessageSerializer<SchematizedType> _target;

            public Base()
            {
                _target = new BrokeredMessageSerializer<SchematizedType>();
            }
        }
    }
}
