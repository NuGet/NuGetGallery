// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Messaging.Tests
{
    public class EmailMessageEnqueuerTests
    {
        public class TheConstructor : FactBase
        {
            [Fact]
            public void NullChecks()
            {
                Assert.Throws<ArgumentNullException>(() => new EmailMessageEnqueuer(null, _serializer.Object, _logger.Object));
                Assert.Throws<ArgumentNullException>(() => new EmailMessageEnqueuer(_topicClient.Object, null, _logger.Object));
                Assert.Throws<ArgumentNullException>(() => new EmailMessageEnqueuer(_topicClient.Object, _serializer.Object, null));
            }
        }

        public class FactBase
        {

            public readonly Mock<ILogger<EmailMessageEnqueuer>> _logger;
            public readonly Mock<IServiceBusMessageSerializer> _serializer;
            public readonly Mock<ITopicClient> _topicClient;
            public EmailMessageData _message;

            public FactBase()
            {
                _logger = new Mock<ILogger<EmailMessageEnqueuer>>();
                _serializer = new Mock<IServiceBusMessageSerializer>();
                _topicClient = new Mock<ITopicClient>();
            }
        }
    }
}
