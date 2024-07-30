// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageChangeEventProcessorTests
    {
        public class TheProcessMethod : MessageChangeEventProcessorTest
        {
            public static IEnumerable<object[]> AllMessageTypes_Data
            {
                get
                {
                    yield return new object[] { MessageType.Start };
                    yield return new object[] { MessageType.End };
                    yield return new object[] { MessageType.Manual };
                }
            }

            public static IEnumerable<object[]> AllImpactedStatuses_Data
            {
                get
                {
                    yield return new object[] { ComponentStatus.Degraded };
                    yield return new object[] { ComponentStatus.Down };
                }
            }

            public static IEnumerable<object[]> AllImpactedStatusesPairs_Data
            {
                get
                {
                    yield return new object[] { ComponentStatus.Degraded, ComponentStatus.Degraded };
                    yield return new object[] { ComponentStatus.Down, ComponentStatus.Degraded };
                    yield return new object[] { ComponentStatus.Degraded, ComponentStatus.Down };
                    yield return new object[] { ComponentStatus.Down, ComponentStatus.Down };
                }
            }

            [Theory]
            [MemberData(nameof(AllMessageTypes_Data))]
            public async Task ReturnsExistingIfUnexpectedPath(MessageType type)
            {
                // Arrange
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    "missingPath",
                    ComponentStatus.Degraded,
                    type);

                var root = new TestComponent("hi");

                var context = new ExistingStartMessageContext(
                    DefaultTimestamp,
                    new TestComponent("name"),
                    ComponentStatus.Down);

                // Act
                var result = await Processor.ProcessAsync(change, EventEntity, root, context);

                // Assert
                Assert.Equal(context, result);
            }

            [Fact]
            public async Task ThrowsWithUnexpectedType()
            {
                var root = new TestComponent("root");

                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.Path,
                    ComponentStatus.Degraded,
                    MessageType.Manual);

                var context = new ExistingStartMessageContext(
                    DefaultTimestamp,
                    new TestComponent("name"),
                    ComponentStatus.Down);

                await Assert.ThrowsAsync<ArgumentException>(() => Processor.ProcessAsync(change, EventEntity, root, context));
            }

            [Fact]
            public async Task IgnoresStartMessageWhereComponentDoesntAffectStatus()
            {
                var hiddenChild = new TestComponent("child");
                var root = new TestComponent("hi", new[] { hiddenChild }, false);

                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.GetByNames<IComponent>(root.Name, hiddenChild.Name).Path,
                    ComponentStatus.Degraded,
                    MessageType.Start);

                var context = new ExistingStartMessageContext(
                    DefaultTimestamp,
                    new TestComponent("name"),
                    ComponentStatus.Down);

                var result = await Processor.ProcessAsync(change, EventEntity, root, context);

                Assert.Equal(context, result);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            It.IsAny<EventEntity>(),
                            It.IsAny<DateTime>(),
                            It.IsAny<MessageType>(),
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatuses_Data))]
            public async Task CreatesStartMessageFromNullContextForHiddenComponent(ComponentStatus status)
            {
                var child = new TestComponent("child");
                var root = new ActivePassiveComponent("hi", "", new[] { child });

                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.GetByNames<IComponent>(root.Name, child.Name).Path,
                    status,
                    MessageType.Start);

                var result = await Processor.ProcessAsync(change, EventEntity, root, null);

                Assert.Equal(change.Timestamp, result.Timestamp);
                Assert.Equal(root, result.AffectedComponent);
                Assert.Equal(root.Status, result.AffectedComponentStatus);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            EventEntity,
                            DefaultTimestamp,
                            MessageType.Start,
                            root),
                        Times.Once());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatuses_Data))]
            public async Task CreatesStartMessageFromNullContext(ComponentStatus status)
            {
                var child = new TestComponent("child");
                var root = new TreeComponent("hi", "", new[] { child });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, child.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    affectedComponent.Path,
                    status,
                    MessageType.Start);

                var result = await Processor.ProcessAsync(change, EventEntity, root, null);

                Assert.Equal(change.Timestamp, result.Timestamp);
                Assert.Equal(affectedComponent, result.AffectedComponent);
                Assert.Equal(affectedComponent.Status, result.AffectedComponentStatus);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            EventEntity,
                            DefaultTimestamp,
                            MessageType.Start,
                            affectedComponent),
                        Times.Once());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatusesPairs_Data))]
            public async Task ThrowsWhenUpdatingStartMessageFromContextWithoutLeastCommonAncestor(ComponentStatus changeStatus, ComponentStatus existingStatus)
            {
                var child = new TestComponent("child");
                var root = new TreeComponent("hi", "", new[] { child });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, child.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    affectedComponent.Path,
                    changeStatus,
                    MessageType.Start);

                var context = new ExistingStartMessageContext(
                    new DateTime(2018, 10, 9), 
                    new TestComponent("no common ancestor"),
                    existingStatus);

                await Assert.ThrowsAsync<ArgumentException>(() => Processor.ProcessAsync(change, EventEntity, root, context));

                Factory
                    .Verify(
                        x => x.UpdateMessageAsync(
                            It.IsAny<EventEntity>(),
                            It.IsAny<DateTime>(),
                            It.IsAny<MessageType>(),
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatusesPairs_Data))]
            public async Task ThrowsWhenUpdatingStartMessageFromContextIfLeastCommonAncestorUnaffected(ComponentStatus changeStatus, ComponentStatus existingStatus)
            {
                var changedChild = new TestComponent("child");
                var existingChild = new TestComponent("existing");
                var root = new AlwaysSameValueTestComponent(
                    ComponentStatus.Up, 
                    "hi", 
                    "", 
                    new[] { changedChild, existingChild });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, changedChild.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    affectedComponent.Path,
                    changeStatus,
                    MessageType.Start);

                var existingAffectedComponent = root.GetByNames<IComponent>(root.Name, existingChild.Name);
                var context = new ExistingStartMessageContext(
                    new DateTime(2018, 10, 9),
                    existingAffectedComponent,
                    existingStatus);

                await Assert.ThrowsAsync<ArgumentException>(() => Processor.ProcessAsync(change, EventEntity, root, context));

                Factory
                    .Verify(
                        x => x.UpdateMessageAsync(
                            It.IsAny<EventEntity>(),
                            It.IsAny<DateTime>(),
                            It.IsAny<MessageType>(),
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatusesPairs_Data))]
            public async Task UpdatesExistingStartMessageFromContext(ComponentStatus changeStatus, ComponentStatus existingStatus)
            {
                var changedChild = new TestComponent("child");
                var existingChild = new TestComponent("existing");
                var root = new TreeComponent("hi", "", new[] { changedChild, existingChild });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, changedChild.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    affectedComponent.Path,
                    changeStatus,
                    MessageType.Start);

                var existingAffectedComponent = root.GetByNames<IComponent>(root.Name, existingChild.Name);
                var context = new ExistingStartMessageContext(
                    new DateTime(2018, 10, 9),
                    existingAffectedComponent,
                    existingStatus);

                var result = await Processor.ProcessAsync(change, EventEntity, root, context);

                Assert.Equal(context.Timestamp, result.Timestamp);
                Assert.Equal(root, result.AffectedComponent);
                Assert.Equal(root.Status, result.AffectedComponentStatus);

                Factory
                    .Verify(
                        x => x.UpdateMessageAsync(
                            EventEntity,
                            context.Timestamp,
                            MessageType.Start,
                            root),
                        Times.Once());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatuses_Data))]
            public async Task IgnoresEndMessageWithNullContext(ComponentStatus changeStatus)
            {
                var root = new TestComponent("root");
                
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.Path,
                    changeStatus,
                    MessageType.End);

                var result = await Processor.ProcessAsync(change, EventEntity, root, null);

                Assert.Null(result);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            It.IsAny<EventEntity>(), 
                            It.IsAny<DateTime>(), 
                            It.IsAny<MessageType>(), 
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatusesPairs_Data))]
            public async Task CreatesEndMessageWithContext(ComponentStatus changeStatus, ComponentStatus existingStatus)
            {
                var child = new TestComponent("child");
                child.Status = changeStatus;
                var root = new ActiveActiveComponent("hi", "", new[] { child });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, child.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp + TimeSpan.FromDays(1),
                    affectedComponent.Path,
                    changeStatus,
                    MessageType.End);

                var context = new ExistingStartMessageContext(
                    DefaultTimestamp,
                    root,
                    existingStatus);

                var result = await Processor.ProcessAsync(change, EventEntity, root, context);

                Assert.Null(result);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            EventEntity,
                            change.Timestamp,
                            MessageType.End,
                            context.AffectedComponent,
                            context.AffectedComponentStatus),
                        Times.Once());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatusesPairs_Data))]
            public async Task IgnoresEndMessageWithContextIfStillAffected(ComponentStatus changeStatus, ComponentStatus existingStatus)
            {
                var child = new TestComponent("child");
                child.Status = changeStatus;
                var root = new AlwaysSameValueTestComponent(
                    ComponentStatus.Degraded, 
                    "hi", 
                    "", 
                    new[] { child }, 
                    false);

                var affectedComponent = root.GetByNames<IComponent>(root.Name, child.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp + TimeSpan.FromDays(1),
                    affectedComponent.Path,
                    changeStatus,
                    MessageType.End);

                var context = new ExistingStartMessageContext(
                    DefaultTimestamp,
                    root,
                    existingStatus);

                var result = await Processor.ProcessAsync(change, EventEntity, root, context);

                Assert.Equal(context, result);

                Factory
                    .Verify(
                        x => x.CreateMessageAsync(
                            It.IsAny<EventEntity>(),
                            It.IsAny<DateTime>(),
                            It.IsAny<MessageType>(),
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            private class AlwaysSameValueTestComponent : Component
            {
                public AlwaysSameValueTestComponent(
                    ComponentStatus value,
                    string name, 
                    string description)
                    : base(name, description)
                {
                    _returnedStatus = value;
                }

                public AlwaysSameValueTestComponent(
                    ComponentStatus value,
                    string name, 
                    string description, 
                    IEnumerable<IComponent> subComponents, 
                    bool displaySubComponents = true)
                    : base(name, description, subComponents, displaySubComponents)
                {
                    _returnedStatus = value;
                }

                private ComponentStatus _returnedStatus;
                private ComponentStatus _internalStatus;
                public override ComponentStatus Status
                {
                    get => _returnedStatus;
                    set
                    {
                        _internalStatus = value;
                    }
                }
            }
        }

        public class MessageChangeEventProcessorTest
        {
            public DateTime DefaultTimestamp = new DateTime(2018, 9, 14);
            public EventEntity EventEntity = new EventEntity();

            public Mock<IMessageFactory> Factory { get; }
            public MessageChangeEventProcessor Processor { get; }

            public MessageChangeEventProcessorTest()
            {
                Factory = new Mock<IMessageFactory>();

                Processor = new MessageChangeEventProcessor(
                    Factory.Object,
                    Mock.Of<ILogger<MessageChangeEventProcessor>>());
            }
        }
    }
}