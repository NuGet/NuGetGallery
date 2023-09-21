// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Messaging.Tests
{
    public class ServiceBusMessageSerializerTests
    {
        private const string SchemaName = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";
        private const string Subject = "Email subject";
        private const string PlainTextBody = "Email plain-text body";
        private const string HtmlBody = "<html><h1>HTML Body</h1></html>";
        private const string Sender = "sender@domain.tld";
        private static readonly IReadOnlyList<string> To = new[] { "to@domain.tld" };
        private static readonly IReadOnlyList<string> CC = new[] { "cc@domain.tld" };
        private static readonly IReadOnlyList<string> BCC = new[] { "bcc@domain.tld" };
        private static readonly IReadOnlyList<string> ReplyTo = new[] { "replyTo@domain.tld" };
        private static readonly Guid MessageTrackingId = new Guid("331FE761-0409-46F4-9EE3-35990E4EBEBB");

        private const string EmailMessageDataType = "EmailMessageData";
        private const int SchemaVersion1 = 1;
        private const int DeliveryCount = 2;

        public class TheSerializeEmailMessageDataMethod : Base
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var input = new EmailMessageData(
                    Subject,
                    PlainTextBody,
                    HtmlBody,
                    Sender,
                    To,
                    CC,
                    BCC,
                    ReplyTo,
                    MessageTrackingId);

                // Act
                var output = _target.SerializeEmailMessageData(input);

                // Assert
                Assert.Contains(SchemaVersionKey, output.Properties.Keys);
                Assert.Equal(SchemaVersion1, output.Properties[SchemaVersionKey]);
                Assert.Contains(SchemaName, output.Properties.Keys);
                Assert.Equal(EmailMessageDataType, output.Properties[SchemaName]);
                var body = output.GetBody();
                Assert.Equal(TestData.SerializedEmailMessageData1, body);
            }

            [Fact]
            public void SetsDefaultTimeToLive()
            {
                // Arrange
                var expectedTtl = TimeSpan.FromDays(2);
                var input = new EmailMessageData(
                    Subject,
                    PlainTextBody,
                    HtmlBody,
                    Sender,
                    To,
                    CC,
                    BCC,
                    ReplyTo,
                    MessageTrackingId);

                // Act
                var output = _target.SerializeEmailMessageData(input);

                // Assert
                Assert.Equal(expectedTtl, output.TimeToLive);
            }
        }

        public class TheDeserializeEmailMessageDataMethod : Base
        {
            private const string TypeValue = "EmailMessageData";

            [Fact]
            public void ProducesExpectedMessage()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();

                // Act
                var output = _target.DeserializeEmailMessageData(brokeredMessage.Object);

                // Assert
                Assert.Equal(Subject, output.Subject);
                Assert.Equal(PlainTextBody, output.PlainTextBody);
                Assert.Equal(HtmlBody, output.HtmlBody);
                Assert.Equal(Sender, output.Sender);
                Assert.Equal(To, output.To);
                Assert.Equal(CC, output.CC);
                Assert.Equal(BCC, output.Bcc);
                Assert.Equal(MessageTrackingId, output.MessageTrackingId);
                Assert.Equal(DeliveryCount, output.DeliveryCount);
            }

            [Fact]
            public void RejectsInvalidType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaName, "bad" },
                    { SchemaVersionKey, SchemaVersion1 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaName} property '{EmailMessageDataType}'.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaName, EmailMessageDataType },
                    { SchemaVersionKey, -1 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message should have {SchemaVersionKey} property '1'.", exception.Message);
            }

            [Fact]
            public void RejectsMissingType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaVersionKey, SchemaVersion1 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaName} property.", exception.Message);
            }

            [Fact]
            public void RejectsMissingSchemaVersion()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaName, EmailMessageDataType },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message does not have a {SchemaVersionKey} property.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidTypeType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaName, -1 },
                    { SchemaVersionKey, SchemaVersion1 },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaName} property that is not a string.", exception.Message);
            }

            [Fact]
            public void RejectsInvalidSchemaVersionType()
            {
                // Arrange
                var brokeredMessage = GetBrokeredMessage();
                brokeredMessage.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaName, EmailMessageDataType },
                    { SchemaVersionKey, "bad" },
                });

                // Act & Assert
                var exception = Assert.Throws<FormatException>(() =>
                    _target.DeserializeEmailMessageData(brokeredMessage.Object));
                Assert.Contains($"The provided message contains a {SchemaVersionKey} property that is not an integer.", exception.Message);
            }

            private static Mock<IReceivedBrokeredMessage> GetBrokeredMessage()
            {
                var brokeredMessage = new Mock<IReceivedBrokeredMessage>();
                brokeredMessage
                    .Setup(x => x.GetBody())
                    .Returns(TestData.SerializedEmailMessageData1);
                brokeredMessage
                    .Setup(x => x.DeliveryCount)
                    .Returns(DeliveryCount);
                brokeredMessage
                    .Setup(x => x.Properties)
                    .Returns(new Dictionary<string, object>
                    {
                        { SchemaName, EmailMessageDataType },
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
