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
    public class IncidentFactoryTests
    {
        public class TheCreateAsyncMethod : IncidentFactoryTest
        {
            [Fact]
            public async Task CreatesEntityAndIncreasesSeverity()
            {
                var input = new ParsedIncident(Incident, "the path", ComponentStatus.Down);

                IncidentEntity entity = null;
                Table
                    .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableEntity>(e =>
                    {
                        Assert.IsType<IncidentEntity>(e);
                        entity = e as IncidentEntity;
                    });

                var group = new IncidentGroupEntity
                {
                    RowKey = "parentRowKey",
                    AffectedComponentStatus = (int)ComponentStatus.Degraded
                };

                Provider
                    .Setup(x => x.GetAsync(input))
                    .ReturnsAsync(group);

                var expectedPath = "the provided path";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(expectedPath);

                var result = await Factory.CreateAsync(input);

                Assert.Equal(entity, result);

                Assert.Equal(input.Id, entity.IncidentApiId);
                Assert.Equal(group.RowKey, entity.ParentRowKey);
                Assert.Equal(expectedPath, entity.AffectedComponentPath);
                Assert.Equal((int)input.AffectedComponentStatus, entity.AffectedComponentStatus);
                Assert.Equal(input.StartTime, entity.StartTime);
                Assert.Equal(input.EndTime, entity.EndTime);
                Assert.Equal((int)input.AffectedComponentStatus, group.AffectedComponentStatus);

                Table
                    .Verify(
                        x => x.InsertOrReplaceAsync(It.IsAny<IncidentEntity>()),
                        Times.Once());

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<IncidentGroupEntity>()),
                        Times.Once());
            }

            [Theory]
            [InlineData(ComponentStatus.Degraded)]
            [InlineData(ComponentStatus.Down)]
            public async Task CreatesEntityAndDoesNotIncreaseSeverity(ComponentStatus existingStatus)
            {
                var input = new ParsedIncident(Incident, "the path", ComponentStatus.Degraded);

                IncidentEntity entity = null;
                Table
                    .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableEntity>(e =>
                    {
                        Assert.IsType<IncidentEntity>(e);
                        entity = e as IncidentEntity;
                    });

                var group = new IncidentGroupEntity
                {
                    RowKey = "parentRowKey",
                    AffectedComponentStatus = (int)existingStatus
                };

                Provider
                    .Setup(x => x.GetAsync(input))
                    .ReturnsAsync(group);

                var expectedPath = "the provided path";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(expectedPath);

                var result = await Factory.CreateAsync(input);

                Assert.Equal(entity, result);

                Assert.Equal(input.Id, entity.IncidentApiId);
                Assert.Equal(group.RowKey, entity.ParentRowKey);
                Assert.Equal(expectedPath, entity.AffectedComponentPath);
                Assert.Equal((int)input.AffectedComponentStatus, entity.AffectedComponentStatus);
                Assert.Equal(input.StartTime, entity.StartTime);
                Assert.Equal(input.EndTime, entity.EndTime);
                Assert.Equal((int)existingStatus, group.AffectedComponentStatus);

                Table
                    .Verify(
                        x => x.InsertOrReplaceAsync(It.IsAny<IncidentEntity>()),
                        Times.Once());

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<IncidentGroupEntity>()),
                        Times.Never());
            }
        }

        public class IncidentFactoryTest
        {
            public Incident Incident = new Incident()
            {
                Id = "some ID",

                Source = new IncidentSourceData
                {
                    CreateDate = new DateTime(2018, 9, 13)
                },

                MitigationData = new IncidentStateChangeEventData
                {
                    Date = new DateTime(2018, 9, 14)
                }
            };

            public Mock<ITableWrapper> Table { get; }
            public Mock<IAggregationProvider<IncidentEntity, IncidentGroupEntity>> Provider { get; }
            public Mock<IAffectedComponentPathProvider<IncidentEntity>> PathProvider { get; }
            public IncidentFactory Factory { get; }

            public IncidentFactoryTest()
            {
                Table = new Mock<ITableWrapper>();

                Provider = new Mock<IAggregationProvider<IncidentEntity, IncidentGroupEntity>>();

                PathProvider = new Mock<IAffectedComponentPathProvider<IncidentEntity>>();

                Factory = new IncidentFactory(
                    Table.Object,
                    Provider.Object,
                    PathProvider.Object,
                    Mock.Of<ILogger<IncidentFactory>>());
            }
        }
    }
}