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
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class AggregationProviderTests
    {
        public class TheIncidentEntityGetAsyncMethod 
            : TheGetAsyncMethod<IncidentEntity, IncidentGroupEntity>
        {
        }

        public class TheIncidentGroupEntityGetAsyncMethod 
            : TheGetAsyncMethod<IncidentGroupEntity, EventEntity>
        {
        }
        
        public abstract class TheGetAsyncMethod<TAggregatedEntity, TEntityAggregation>
            : AggregationProviderTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedComponentAffectingEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task CreatesNewEntityIfNoPossibleAggregation()
            {
                var inputPath = "howdy";
                var input = new ParsedIncident(Incident, inputPath, ComponentStatus.Degraded);

                var providedPath = "hello";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(providedPath);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime,
                    EndTime = input.EndTime
                };
                
                Table.SetupQuery(
                    aggregationWithDifferentPath,
                    aggregationAfter,
                    aggregationBefore,
                    activeAggregationToDeactivate,
                    inactiveAggregationToDeactivate);

                Strategy
                    .Setup(x => x.CanBeAggregatedByAsync(input, activeAggregationToDeactivate))
                    .ReturnsAsync(false);

                Strategy
                    .Setup(x => x.CanBeAggregatedByAsync(input, inactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                var createdAggregation = new TEntityAggregation();
                AggregationFactory
                    .Setup(x => x.CreateAsync(input))
                    .ReturnsAsync(createdAggregation);

                var result = await Provider.GetAsync(input);

                Assert.Equal(createdAggregation, result);

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationAfter),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationBefore),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), activeAggregationToDeactivate),
                        Times.Once());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), inactiveAggregationToDeactivate),
                        Times.Once());
            }

            [Fact]
            public async Task ReturnsPossibleAggregation()
            {
                var inputPath = "howdy";
                var input = new ParsedIncident(Incident, inputPath, ComponentStatus.Degraded);

                var providedPath = "hello";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(providedPath);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime,
                    EndTime = input.EndTime
                };

                var activeAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };
                
                Table.SetupQuery(
                    aggregationWithDifferentPath,
                    aggregationAfter,
                    aggregationBefore,
                    activeAggregationToDeactivate,
                    inactiveAggregationToDeactivate,
                    activeAggregation);

                Strategy
                    .Setup(x => x.CanBeAggregatedByAsync(input, activeAggregationToDeactivate))
                    .ReturnsAsync(false);

                Strategy
                    .Setup(x => x.CanBeAggregatedByAsync(input, inactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Strategy
                    .Setup(x => x.CanBeAggregatedByAsync(input, activeAggregation))
                    .ReturnsAsync(true);

                var result = await Provider.GetAsync(input);

                Assert.Equal(activeAggregation, result);

                AggregationFactory
                    .Verify(
                        x => x.CreateAsync(input),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationAfter),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), aggregationBefore),
                        Times.Never());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), activeAggregationToDeactivate),
                        Times.Once());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), inactiveAggregationToDeactivate),
                        Times.Once());

                Strategy
                    .Verify(
                        x => x.CanBeAggregatedByAsync(It.IsAny<ParsedIncident>(), activeAggregation),
                        Times.Once());
            }
        }

        public class AggregationProviderTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedComponentAffectingEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            public Incident Incident = new Incident()
            {
                Source = new IncidentSourceData()
                {
                    CreateDate = new DateTime(2018, 9, 13)
                }
            };

            public Mock<ITableWrapper> Table { get; }
            public Mock<IComponentAffectingEntityFactory<TEntityAggregation>> AggregationFactory { get; }
            public Mock<IAffectedComponentPathProvider<TEntityAggregation>> PathProvider { get; }
            public Mock<IAggregationStrategy<TEntityAggregation>> Strategy { get; }

            public AggregationProvider<TAggregatedEntity, TEntityAggregation> Provider { get; }

            public AggregationProviderTest()
            {
                Table = new Mock<ITableWrapper>();

                AggregationFactory = new Mock<IComponentAffectingEntityFactory<TEntityAggregation>>();

                PathProvider = new Mock<IAffectedComponentPathProvider<TEntityAggregation>>();

                Strategy = new Mock<IAggregationStrategy<TEntityAggregation>>();

                Provider = new AggregationProvider<TAggregatedEntity, TEntityAggregation>(
                    Table.Object,
                    PathProvider.Object,
                    Strategy.Object,
                    AggregationFactory.Object,
                    Mock.Of<ILogger<AggregationProvider<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}