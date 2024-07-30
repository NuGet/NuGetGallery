// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using StatusAggregator.Factory;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class ComponentExporterTests
    {
        public class TheExportMethod : ComponentExporterTest
        {
            [Fact]
            public void ReturnsUnaffectedComponentTreeWithNoEntities()
            {
                Table.SetupQuery<MessageEntity>();
                Table.SetupQuery<IncidentGroupEntity>();
                Table.SetupQuery<EventEntity>();

                var result = Exporter.Export();

                Assert.All(
                    result.GetAllVisibleComponents(), 
                    c => Assert.Equal(ComponentStatus.Up, c.Status));
            }

            [Fact]
            public void AppliesActiveEntitiesToComponentTree()
            {
                var eventWithMessage = new EventEntity(Level2A.Path, DefaultStartTime, ComponentStatus.Degraded);
                var messageForEventWithMessage = new MessageEntity(eventWithMessage, DefaultStartTime, "", MessageType.Manual);
                var degradedIncidentGroupForEventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Degraded, DefaultStartTime);
                var downIncidentGroupForEventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Down, DefaultStartTime);
                var upIncidentGroupForEventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Up, DefaultStartTime);
                var inactiveIncidentGroupForEventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3BFrom2A.Path, ComponentStatus.Degraded, DefaultStartTime, DefaultStartTime);
                var missingPathIncidentGroupForEventWithMessage = new IncidentGroupEntity(eventWithMessage, "missingPath", ComponentStatus.Degraded, DefaultStartTime);

                var eventWithoutMessage = new EventEntity(Level2B.Path, DefaultStartTime, ComponentStatus.Degraded);
                var incidentGroupForEventWithoutMessage = new IncidentGroupEntity(eventWithoutMessage, Level3AFrom2B.Path, ComponentStatus.Degraded, DefaultStartTime);

                var inactiveEventWithMessage = new EventEntity(Level2B.Path, DefaultStartTime + TimeSpan.FromDays(1), ComponentStatus.Degraded, DefaultStartTime + TimeSpan.FromDays(2));
                var messageForInactiveEventWithMessage = new MessageEntity(inactiveEventWithMessage, DefaultStartTime + TimeSpan.FromDays(1), "", MessageType.Manual);
                var incidentGroupForInactiveEventWithMessage = new IncidentGroupEntity(inactiveEventWithMessage, Level3BFrom2B.Path, ComponentStatus.Degraded, DefaultStartTime + TimeSpan.FromDays(1));

                Table.SetupQuery(
                    messageForEventWithMessage, 
                    messageForInactiveEventWithMessage);

                Table.SetupQuery(
                    degradedIncidentGroupForEventWithMessage, 
                    downIncidentGroupForEventWithMessage, 
                    upIncidentGroupForEventWithMessage, 
                    inactiveIncidentGroupForEventWithMessage, 
                    missingPathIncidentGroupForEventWithMessage,
                    incidentGroupForEventWithoutMessage, 
                    incidentGroupForInactiveEventWithMessage);

                Table.SetupQuery(
                    eventWithMessage, 
                    eventWithoutMessage, 
                    inactiveEventWithMessage);

                var result = Exporter.Export();

                // Status of events with messages are applied.
                AssertComponentStatus(ComponentStatus.Degraded, Level2A, eventWithMessage);

                // Most severe status affecting same component is applied.
                AssertComponentStatus(
                    ComponentStatus.Down,
                    Level3AFrom2A, 
                    degradedIncidentGroupForEventWithMessage, 
                    downIncidentGroupForEventWithMessage, 
                    upIncidentGroupForEventWithMessage);

                // Status of inactive incident groups are not applied.
                AssertComponentStatus(ComponentStatus.Up, Level3BFrom2A, inactiveIncidentGroupForEventWithMessage);

                // Status of events without messages are not applied.
                // Status of inactive events with messages are not applied.
                AssertComponentStatus(ComponentStatus.Up, Level2B, eventWithoutMessage, inactiveEventWithMessage);

                // Status of incident groups for events without messages are not applied.
                AssertComponentStatus(ComponentStatus.Up, Level3AFrom2B, incidentGroupForEventWithoutMessage);

                // Status of incident groups for inactive events with messages are not applied.
                AssertComponentStatus(ComponentStatus.Up, Level3BFrom2B, incidentGroupForInactiveEventWithMessage);
            }
            
            private void AssertComponentStatus(ComponentStatus expected, IComponent component, params ComponentAffectingEntity[] entities)
            {
                Assert.Equal(expected, component.Status);
                Assert.Equal(component.Path, entities.First().AffectedComponentPath);

                for (var i = 1; i < entities.Count(); i++)
                {
                    Assert.Equal(
                        entities[i - 1].AffectedComponentPath, 
                        entities[i].AffectedComponentPath);
                }
            }
        }

        public class ComponentExporterTest
        {
            public DateTime DefaultStartTime => new DateTime(2018, 9, 12);

            public IComponent Root { get; }
            public IComponent Level2A { get; }
            public IComponent Level2B { get; }
            public IComponent Level3AFrom2A { get; }
            public IComponent Level3BFrom2A { get; }
            public IComponent Level3AFrom2B { get; }
            public IComponent Level3BFrom2B { get; }

            public Mock<IComponentFactory> Factory { get; }
            public Mock<ITableWrapper> Table { get; }
            public ComponentExporter Exporter { get; }

            public ComponentExporterTest()
            {
                var level3AFrom2A = new TestComponent("3A");
                var level3BFrom2A = new TestComponent("3B");
                var level2A = new TestComponent("2A", 
                    new[] { level3AFrom2A, level3BFrom2A });

                var level3AFrom2B = new TestComponent("3A");
                var level3BFrom2B = new TestComponent("3B");
                var level2B = new TestComponent("2B", 
                    new[] { level3AFrom2B, level3BFrom2B });

                Root = new TestComponent("Root", new[] { level2A, level2B });

                // We have to get the subcomponents by iterating through the tree. 
                // Components only have a path in the context of accessing them through a parent.
                Level2A = Root
                    .SubComponents.Single(c => c.Name == "2A");
                Level3AFrom2A = Root
                    .SubComponents.Single(c => c.Name == "2A")
                    .SubComponents.Single(c => c.Name == "3A");
                Level3BFrom2A = Root
                    .SubComponents.Single(c => c.Name == "2A")
                    .SubComponents.Single(c => c.Name == "3B");

                Level2B = Root
                    .SubComponents.Single(c => c.Name == "2B");
                Level3AFrom2B = Root
                    .SubComponents.Single(c => c.Name == "2B")
                    .SubComponents.Single(c => c.Name == "3A");
                Level3BFrom2B = Root
                    .SubComponents.Single(c => c.Name == "2B")
                    .SubComponents.Single(c => c.Name == "3B");

                Factory = new Mock<IComponentFactory>();
                Factory
                    .Setup(x => x.Create())
                    .Returns(Root);

                Table = new Mock<ITableWrapper>();

                Exporter = new ComponentExporter(
                    Table.Object,
                    Factory.Object,
                    Mock.Of<ILogger<ComponentExporter>>());
            }
        }
    }
}