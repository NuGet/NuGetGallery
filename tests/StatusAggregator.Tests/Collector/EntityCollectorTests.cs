// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using StatusAggregator.Collector;
using Xunit;

namespace StatusAggregator.Tests.Collector
{
    public class EntityCollectorTests
    {
        public class TheNameProperty : EntityCollectorTest
        {
            [Fact]
            public void ReturnsProcessorName()
            {
                Assert.Equal(Name, Collector.Name);
            }
        }

        public class TheFetchLatestMethod : EntityCollectorTest
        {
            [Fact]
            public async Task DoesNotSetValueIfProcessorReturnsNull()
            {
                Processor
                    .Setup(x => x.FetchSince(LastCursor))
                    .ReturnsAsync((DateTime?)null);

                var result = await Collector.FetchLatest();

                Assert.Equal(LastCursor, result);

                Cursor
                    .Verify(
                        x => x.Set(It.IsAny<string>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task SetsValueIfProcessorReturnsValue()
            {
                var nextCursor = new DateTime(2018, 9, 12);

                Processor
                    .Setup(x => x.FetchSince(LastCursor))
                    .ReturnsAsync(nextCursor);

                Cursor
                    .Setup(x => x.Set(Name, nextCursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var result = await Collector.FetchLatest();

                Assert.Equal(nextCursor, result);

                Cursor.Verify();
            }
        }

        public class EntityCollectorTest
        {
            public string Name => "name";
            public DateTime LastCursor => new DateTime(2018, 9, 11);
            public Mock<ICursor> Cursor { get; }
            public Mock<IEntityCollectorProcessor> Processor { get; }
            public EntityCollector Collector { get; }

            public EntityCollectorTest()
            {
                Cursor = new Mock<ICursor>();
                Cursor
                    .Setup(x => x.Get(Name))
                    .ReturnsAsync(LastCursor);

                Processor = new Mock<IEntityCollectorProcessor>();
                Processor
                    .Setup(x => x.Name)
                    .Returns(Name);

                Collector = new EntityCollector(
                    Cursor.Object, 
                    Processor.Object);
            }
        }
    }
}