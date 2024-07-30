// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Messages;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageChangeEventIteratorTests
    {
        public class TheIterateMethod : MessageChangeEventIteratorTest
        {
            [Fact]
            public async Task IteratesChangesInOrder()
            {
                var eventEntity = new EventEntity();

                var root = new TestComponent("hi");
                Factory
                    .Setup(x => x.Create())
                    .Returns(root);

                var firstChange = CreateChangeEvent(TimeSpan.Zero);
                var secondChange = CreateChangeEvent(TimeSpan.FromDays(1));
                var thirdChange = CreateChangeEvent(TimeSpan.FromDays(2));
                var changes = new[] { thirdChange, firstChange, secondChange };

                var firstComponent = new TestComponent("first");
                var firstContext = new ExistingStartMessageContext(firstChange.Timestamp, firstComponent, ComponentStatus.Degraded);
                Processor
                    .Setup(x => x.ProcessAsync(firstChange, eventEntity, root, null))
                    .ReturnsAsync(firstContext)
                    .Verifiable();

                var secondComponent = new TestComponent("second");
                var secondContext = new ExistingStartMessageContext(secondChange.Timestamp, secondComponent, ComponentStatus.Degraded);
                Processor
                    .Setup(x => x.ProcessAsync(secondChange, eventEntity, root, firstContext))
                    .ReturnsAsync(secondContext)
                    .Verifiable();

                var thirdComponent = new TestComponent("third");
                var thirdContext = new ExistingStartMessageContext(thirdChange.Timestamp, thirdComponent, ComponentStatus.Degraded);
                Processor
                    .Setup(x => x.ProcessAsync(thirdChange, eventEntity, root, secondContext))
                    .ReturnsAsync(thirdContext)
                    .Verifiable();

                await Iterator.IterateAsync(changes, eventEntity);

                Processor.Verify();

                Processor
                    .Verify(
                        x => x.ProcessAsync(
                            It.IsAny<MessageChangeEvent>(),
                            It.IsAny<EventEntity>(),
                            It.IsAny<IComponent>(),
                            It.IsAny<ExistingStartMessageContext>()),
                        Times.Exactly(3));
            }

            private MessageChangeEvent CreateChangeEvent(TimeSpan offset)
            {
                return new MessageChangeEvent(
                    new DateTime(2018, 9, 14) + offset,
                    "",
                    ComponentStatus.Up,
                   MessageType.Manual);
            }
        }

        public class MessageChangeEventIteratorTest
        {
            public Mock<IComponentFactory> Factory { get; }
            public Mock<IMessageChangeEventProcessor> Processor { get; }
            public MessageChangeEventIterator Iterator { get; }

            public MessageChangeEventIteratorTest()
            {
                Factory = new Mock<IComponentFactory>();

                Processor = new Mock<IMessageChangeEventProcessor>();

                Iterator = new MessageChangeEventIterator(
                    Factory.Object,
                    Processor.Object,
                    Mock.Of<ILogger<MessageChangeEventIterator>>());
            }
        }
    }
}