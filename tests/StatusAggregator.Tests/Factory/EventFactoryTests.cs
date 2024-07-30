// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class EventFactoryTests
    {
        public class TheCreateAsyncMethod : EventFactoryTest
        {
            [Fact]
            public async Task CreatesEvent()
            {
                var input = new ParsedIncident(Incident, "somePath", ComponentStatus.Up);

                EventEntity entity = null;
                Table
                    .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableEntity>(e =>
                    {
                        Assert.IsType<EventEntity>(e);
                        entity = e as EventEntity;
                    });

                var aggregationPath = "thePath";
                Provider
                    .Setup(x => x.Get(input))
                    .Returns(aggregationPath);

                var result = await Factory.CreateAsync(input);

                Assert.Equal(entity, result);
                Assert.Equal(aggregationPath, entity.AffectedComponentPath);
                Assert.Equal((int)ComponentStatus.Up, entity.AffectedComponentStatus);
                Assert.Equal(input.StartTime, entity.StartTime);

                Table
                    .Verify(
                        x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Once());
            }
        }

        public class EventFactoryTest
        {
            public Incident Incident = new Incident() {
                Source = new IncidentSourceData() {
                    CreateDate = new DateTime(2018, 9, 13) } };

            public Mock<ITableWrapper> Table { get; }
            public Mock<IAffectedComponentPathProvider<EventEntity>> Provider { get; }
            public EventFactory Factory { get; }

            public EventFactoryTest()
            {
                Table = new Mock<ITableWrapper>();

                Provider = new Mock<IAffectedComponentPathProvider<EventEntity>>();

                Factory = new EventFactory(
                    Table.Object,
                    Provider.Object,
                    Mock.Of<ILogger<EventFactory>>());
            }
        }
    }
}