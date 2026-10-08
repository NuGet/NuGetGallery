// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using Xunit;

namespace NuGetGallery
{
    /// <summary>
    /// Verifies deferred promotion messaging does not require a connection for read-only staging management.
    /// </summary>
    public class StagingPromotionMessageEnqueuerFacts
    {
        [Fact]
        public async Task CreatesTheClientOnFirstSendAndReusesIt()
        {
            var message = StagingPromotionMessage.ForPackage(Guid.NewGuid(), 42);
            var serialized = Mock.Of<IBrokeredMessage>();
            var serializer = new Mock<IBrokeredMessageSerializer<StagingPromotionMessage>>();
            serializer.Setup(x => x.Serialize(message)).Returns(serialized);
            var client = new Mock<ITopicClient>();
            client.Setup(x => x.SendAsync(serialized)).Returns(Task.CompletedTask);
            var deferred = new Lazy<ITopicClient>(() => client.Object);
            var target = new StagingPromotionMessageEnqueuer(deferred, serializer.Object);
            Assert.False(deferred.IsValueCreated);

            await target.SendMessageAsync(message);
            await target.SendMessageAsync(message);

            Assert.True(deferred.IsValueCreated);
            client.Verify(x => x.SendAsync(serialized), Times.Exactly(2));
            serializer.Verify(x => x.Serialize(message), Times.Exactly(2));
        }
    }
}
