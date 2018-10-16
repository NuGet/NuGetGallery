// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class EventMessagingUpdaterTests
    {
        public class TheUpdateAsyncMethod : EventMessagingUpdaterTest
        {
            [Fact]
            public void GetsAndIteratesChanges()
            {
                var eventEntity = new EventEntity();
                var cursor = new DateTime(2018, 10, 9);

                var changes = new MessageChangeEvent[] {
                    new MessageChangeEvent(new DateTime(2018, 10, 9), "path", ComponentStatus.Degraded, MessageType.Start) };

                Provider
                    .Setup(x => x.Get(eventEntity, cursor))
                    .Returns(changes);

                var iteratorTask = Task.FromResult("something to make this task unique");
                Iterator
                    .Setup(x => x.IterateAsync(changes, eventEntity))
                    .Returns(iteratorTask);

                var result = Updater.UpdateAsync(eventEntity, cursor);

                Assert.Equal(iteratorTask, result);
            }
        }

        public class EventMessagingUpdaterTest
        {
            public Mock<IMessageChangeEventProvider> Provider { get; }
            public Mock<IMessageChangeEventIterator> Iterator { get; }
            
            public EventMessagingUpdater Updater { get; }

            public EventMessagingUpdaterTest()
            {
                Provider = new Mock<IMessageChangeEventProvider>();

                Iterator = new Mock<IMessageChangeEventIterator>();

                Updater = new EventMessagingUpdater(
                    Provider.Object,
                    Iterator.Object,
                    Mock.Of<ILogger<EventMessagingUpdater>>());
            }
        }
    }
}
