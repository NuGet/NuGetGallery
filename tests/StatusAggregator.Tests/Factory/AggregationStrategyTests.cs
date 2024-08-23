// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class AggregationStrategyTests
    {
        public class TheIncidentEntityCanBeAggregatedByAsyncMethod 
            : TheCanBeAggregatedByAsyncMethod<IncidentEntity, IncidentGroupEntity>
        {
        }

        public class TheIncidentGroupEntityCanBeAggregatedByAsyncMethod 
            : TheCanBeAggregatedByAsyncMethod<IncidentGroupEntity, EventEntity>
        {
        }

        public abstract class TheCanBeAggregatedByAsyncMethod<TAggregatedEntity, TEntityAggregation>
            : AggregationStrategyTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedComponentAffectingEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task ReturnsFalseIfNoLinkedEntities()
            {
                var aggregatedEntityLinkedToDifferentAggregation = new TAggregatedEntity
                {
                    ParentRowKey = "wrongRowKey"
                };

                Table.SetupQuery(aggregatedEntityLinkedToDifferentAggregation);

                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 9, 13)
                    }
                };

                var input = new ParsedIncident(incident, "", ComponentStatus.Up);

                var result = await Strategy.CanBeAggregatedByAsync(input, Aggregation);

                Assert.False(result);

                Updater
                    .Verify(
                        x => x.UpdateAsync(It.IsAny<TEntityAggregation>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsTrueIfAggregationStillActive(bool inputIsActive)
            {
                var aggregatedEntity = new TAggregatedEntity
                {
                    ParentRowKey = AggregationRowKey
                };

                Table.SetupQuery(aggregatedEntity);

                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 9, 13)
                    }
                };

                if (!inputIsActive)
                {
                    incident.MitigationData = new IncidentStateChangeEventData()
                    {
                        Date = new DateTime(2018, 10, 9)
                    };
                }

                var input = new ParsedIncident(incident, "", ComponentStatus.Up);

                Updater
                    .Setup(x => x.UpdateAsync(Aggregation, input.StartTime))
                    .Returns(Task.CompletedTask);

                var result = await Strategy.CanBeAggregatedByAsync(input, Aggregation);

                Assert.True(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsTrueIfInputInactive(bool aggregationIsActive)
            {
                var aggregatedEntity = new TAggregatedEntity
                {
                    ParentRowKey = AggregationRowKey
                };

                if (!aggregationIsActive)
                {
                    Aggregation.EndTime = new DateTime(2018, 10, 9);
                }

                Table.SetupQuery(aggregatedEntity);

                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 9, 13)
                    },

                    MitigationData = new IncidentStateChangeEventData()
                    {
                        Date = new DateTime(2018, 10, 9)
                    }
                };

                var input = new ParsedIncident(incident, "", ComponentStatus.Up);

                Updater
                    .Setup(x => x.UpdateAsync(Aggregation, input.StartTime))
                    .Returns(Task.CompletedTask);

                var result = await Strategy.CanBeAggregatedByAsync(input, Aggregation);

                Assert.True(result);
            }

            [Fact]
            public async Task ReturnsFalseIfAggregationInactiveAndInputActive()
            {
                Aggregation.EndTime = new DateTime(2018, 10, 9);

                var aggregatedEntity = new TAggregatedEntity
                {
                    ParentRowKey = AggregationRowKey
                };

                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 9, 13)
                    }
                };

                var input = new ParsedIncident(incident, "", ComponentStatus.Up);

                Table.SetupQuery(aggregatedEntity);

                Updater
                    .Setup(x => x.UpdateAsync(Aggregation, input.StartTime))
                    .Returns(Task.CompletedTask);

                var result = await Strategy.CanBeAggregatedByAsync(input, Aggregation);

                Assert.False(result);
            }
        }

        public class AggregationStrategyTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedComponentAffectingEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            public const string AggregationRowKey = "aggregationRowKey";
            public TEntityAggregation Aggregation = new TEntityAggregation()
            {
                RowKey = AggregationRowKey
            };

            public Mock<ITableWrapper> Table { get; }
            public Mock<IComponentAffectingEntityUpdater<TEntityAggregation>> Updater { get; }
            public AggregationStrategy<TAggregatedEntity, TEntityAggregation> Strategy { get; }

            public AggregationStrategyTest()
            {
                Table = new Mock<ITableWrapper>();

                Updater = new Mock<IComponentAffectingEntityUpdater<TEntityAggregation>>();

                Strategy = new AggregationStrategy<TAggregatedEntity, TEntityAggregation>(
                    Table.Object,
                    Updater.Object,
                    Mock.Of<ILogger<AggregationStrategy<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}