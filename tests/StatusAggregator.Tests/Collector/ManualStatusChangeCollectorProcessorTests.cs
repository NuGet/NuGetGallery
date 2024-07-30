// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Collector;
using StatusAggregator.Manual;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using Xunit;

namespace StatusAggregator.Tests.Collector
{
    public class ManualStatusChangeCollectorProcessorTests
    {
        public class TheNameProperty : ManualStatusChangeCollectorTest
        {
            [Fact]
            public void ReturnsManualCollectorNamePrefixConcatenation()
            {
                Assert.Equal(
                    ManualStatusChangeCollectorProcessor.ManualCollectorNamePrefix + Name, 
                    Processor.Name);
            }
        }

        public abstract class TheFetchSinceMethod : ManualStatusChangeCollectorTest
        {
            public abstract DateTime Cursor { get; }

            [Fact]
            public async Task ReturnsNullIfNoManualChanges()
            {
                Table.SetupQuery<ManualStatusChangeEntity>();

                var result = await Processor.FetchSince(Cursor);

                Assert.Null(result);

                Handler
                    .Verify(
                        x => x.Handle(It.IsAny<ITableWrapper>(), It.IsAny<ManualStatusChangeEntity>()),
                        Times.Never());
            }

            [Fact]
            public async Task HandlesManualChanges()
            {
                var firstChange = new ManualStatusChangeEntity()
                {
                    Timestamp = Cursor.ToUniversalTime() + TimeSpan.FromMinutes(1)
                };

                var secondChange = new ManualStatusChangeEntity()
                {
                    Timestamp = Cursor.ToUniversalTime() + TimeSpan.FromMinutes(2)
                };
                
                Table.SetupQuery(secondChange, firstChange);

                var lastTimestamp = DateTimeOffset.MinValue;
                Handler
                    .Setup(x => x.Handle(Table.Object, It.IsAny<ManualStatusChangeEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableWrapper, ManualStatusChangeEntity>((table, entity) =>
                    {
                        var nextTimestamp = entity.Timestamp;
                        Assert.True(nextTimestamp > lastTimestamp);
                        lastTimestamp = nextTimestamp;
                    });

                var result = await Processor.FetchSince(Cursor);

                Assert.Equal(secondChange.Timestamp.UtcDateTime, result);

                Handler
                    .Verify(
                        x => x.Handle(Table.Object, firstChange),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.Handle(Table.Object, secondChange),
                        Times.Once());
            }
        }

        public class TheFetchSinceMethodAtMinValue : TheFetchSinceMethod
        {
            public override DateTime Cursor => DateTime.MinValue;

            [Fact]
            public async Task DoesNotFilterByCursor()
            {
                var change = new ManualStatusChangeEntity()
                {
                    Timestamp = DateTimeOffset.MinValue
                };
                
                Table.SetupQuery(change);

                Handler
                    .Setup(x => x.Handle(Table.Object, change))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var result = await Processor.FetchSince(Cursor);

                Assert.Equal(change.Timestamp.UtcDateTime, result);

                Handler.Verify();
            }
        }

        public class TheFetchSinceMethodAtPresent : TheFetchSinceMethod
        {
            public override DateTime Cursor => new DateTime(2018, 9, 12, 0, 0, 0, DateTimeKind.Utc);
        }

        public class ManualStatusChangeCollectorTest
        {
            public string Name => "name";
            public Mock<ITableWrapper> Table { get; }
            public Mock<IManualStatusChangeHandler> Handler { get; }
            public ManualStatusChangeCollectorProcessor Processor { get; }

            public ManualStatusChangeCollectorTest()
            {
                Table = new Mock<ITableWrapper>();
                Handler = new Mock<IManualStatusChangeHandler>();

                Processor = new ManualStatusChangeCollectorProcessor(
                    Name,
                    Table.Object,
                    Handler.Object,
                    Mock.Of<ILogger<ManualStatusChangeCollectorProcessor>>());
            }
        }
    }
}