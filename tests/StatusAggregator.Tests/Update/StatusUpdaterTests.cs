// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StatusAggregator.Collector;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class StatusUpdaterTests
    {
        public class TheUpdateMethod
            : StatusUpdaterTest
        {
            [Fact]
            public async Task DoesNotUpdateCursorIfIncidentCollectorFails()
            {
                ManualStatusChangeCollector1
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 12))
                    .Verifiable();

                ManualStatusChangeCollector2
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 13))
                    .Verifiable();

                IncidentCollector
                    .Setup(x => x.FetchLatest())
                    .ThrowsAsync(new Exception())
                    .Verifiable();

                var cursor = new DateTime(2018, 11, 12);
                await Updater.Update(cursor);

                ManualStatusChangeCollector1.Verify();
                ManualStatusChangeCollector2.Verify();
                IncidentCollector.Verify();

                ActiveEventEntityUpdater
                    .Verify(
                        x => x.UpdateAllAsync(It.IsAny<DateTime>()),
                        Times.Never());

                Cursor
                    .Verify(
                        x => x.Set(It.IsAny<string>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task DoesNotUpdateCursorIfActiveEventUpdaterFails()
            {
                ManualStatusChangeCollector1
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 12))
                    .Verifiable();

                ManualStatusChangeCollector2
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 13))
                    .Verifiable();

                IncidentCollector
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 14))
                    .Verifiable();

                var cursor = new DateTime(2018, 11, 12);
                ActiveEventEntityUpdater
                    .Setup(x => x.UpdateAllAsync(cursor))
                    .ThrowsAsync(new Exception())
                    .Verifiable();
                
                await Updater.Update(cursor);

                ManualStatusChangeCollector1.Verify();
                ManualStatusChangeCollector2.Verify();
                IncidentCollector.Verify();
                ActiveEventEntityUpdater.Verify();

                Cursor
                    .Verify(
                        x => x.Set(It.IsAny<string>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task UpdatesCursorIfSuccessful()
            {
                ManualStatusChangeCollector1
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 12))
                    .Verifiable();

                ManualStatusChangeCollector2
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 13))
                    .Verifiable();

                IncidentCollector
                    .Setup(x => x.FetchLatest())
                    .ReturnsAsync(new DateTime(2018, 11, 14))
                    .Verifiable();

                var cursor = new DateTime(2018, 11, 12);
                ActiveEventEntityUpdater
                    .Setup(x => x.UpdateAllAsync(cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                Cursor
                    .Setup(x => x.Set(StatusUpdater.LastUpdatedCursorName, cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.Update(cursor);

                ManualStatusChangeCollector1.Verify();
                ManualStatusChangeCollector2.Verify();
                IncidentCollector.Verify();
                ActiveEventEntityUpdater.Verify();
                Cursor.Verify();
            }
        }

        public class StatusUpdaterTest
        {
            public Mock<ICursor> Cursor { get; }
            public Mock<IEntityCollector> IncidentCollector { get; }
            public Mock<IEntityCollector> ManualStatusChangeCollector1 { get; }
            public Mock<IEntityCollector> ManualStatusChangeCollector2 { get; }
            public Mock<IActiveEventEntityUpdater> ActiveEventEntityUpdater { get; }

            public StatusUpdater Updater { get; }

            public StatusUpdaterTest()
            {
                Cursor = new Mock<ICursor>();
                IncidentCollector = CreateCollectorWithName(
                    IncidentEntityCollectorProcessor.IncidentsCollectorName);
                ManualStatusChangeCollector1 = CreateCollectorWithName(
                    ManualStatusChangeCollectorProcessor.ManualCollectorNamePrefix + "1");
                ManualStatusChangeCollector2 = CreateCollectorWithName(
                    ManualStatusChangeCollectorProcessor.ManualCollectorNamePrefix + "2");
                ActiveEventEntityUpdater = new Mock<IActiveEventEntityUpdater>();

                Updater = new StatusUpdater(
                    Cursor.Object,
                    new[] 
                    {
                        IncidentCollector,
                        ManualStatusChangeCollector1,
                        ManualStatusChangeCollector2
                    }.Select(x => x.Object),
                    ActiveEventEntityUpdater.Object,
                    Mock.Of<ILogger<StatusUpdater>>());
            }
        }

        public class TheConstructor
        {
            [Fact]
            public void ThrowsWithoutCursor()
            {
                var incidentCollector = new Mock<IEntityCollector>();
                incidentCollector
                    .Setup(x => x.Name)
                    .Returns(IncidentEntityCollectorProcessor.IncidentsCollectorName);

                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        null,
                        new[] { incidentCollector.Object },
                        Mock.Of<IActiveEventEntityUpdater>(),
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            public static IEnumerable<object[]> ThrowsWithoutCollectors_Data
            {
                get
                {
                    // null enumerable
                    yield return new object[] { typeof(ArgumentNullException), null };

                    // empty enumerable
                    yield return new object[] { typeof(ArgumentException), new IEntityCollector[0] };

                    // enumerable without incident collector
                    yield return new object[] { typeof(ArgumentException), new[] { CreateCollectorWithName("howdy").Object } };
                }
            }

            [Theory]
            [MemberData(nameof(ThrowsWithoutCollectors_Data))]
            public void ThrowsWithoutCollectors(Type exceptionType, IEnumerable<IEntityCollector> collectors)
            {
                Assert.Throws(
                    exceptionType,
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        collectors,
                        Mock.Of<IActiveEventEntityUpdater>(),
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            [Fact]
            public void ThrowsWithoutActiveEventUpdater()
            {
                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        new[]
                        {
                            CreateCollectorWithName(IncidentEntityCollectorProcessor.IncidentsCollectorName).Object
                        },
                        null,
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            [Fact]
            public void ThrowsWithoutLogger()
            {
                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        new[] 
                        {
                            CreateCollectorWithName(IncidentEntityCollectorProcessor.IncidentsCollectorName).Object
                        },
                        Mock.Of<IActiveEventEntityUpdater>(),
                        null));
            }
        }

        private static Mock<IEntityCollector> CreateCollectorWithName(string name)
        {
            var collector = new Mock<IEntityCollector>();
            collector
                .Setup(x => x.Name)
                .Returns(name);

            return collector;
        }
    }
}
