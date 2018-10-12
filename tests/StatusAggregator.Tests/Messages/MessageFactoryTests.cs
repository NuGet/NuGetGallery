// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageFactoryTests
    {
        public class TheCreateMessageAsyncMethodWithImplicitStatus
            : TheCreateMessageAsyncMethod
        {
            protected override Task InvokeMethod(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status)
            {
                return Factory.CreateMessageAsync(eventEntity, time, type, component);
            }

            protected override ComponentStatus GetExpectedStatus(IComponent component, ComponentStatus status)
            {
                return component.Status;
            }
        }

        public class TheCreateMessageAsyncMethodWithExplicitStatus
            : TheCreateMessageAsyncMethod
        {
            protected override Task InvokeMethod(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status)
            {
                return Factory.CreateMessageAsync(eventEntity, time, type, component, status);
            }

            protected override ComponentStatus GetExpectedStatus(IComponent component, ComponentStatus status)
            {
                return status;
            }
        }

        public abstract class TheCreateMessageAsyncMethod
            : MessageFactoryTest
        {
            [Fact]
            public async Task ReturnsExistingMessage()
            {
                var type = (MessageType)99;
                var status = (ComponentStatus)100;
                var componentStatus = (ComponentStatus)101;
                var component = new TestComponent("component")
                {
                    Status = componentStatus
                };

                var existingMessage = new MessageEntity(EventEntity, Time, "existing", (MessageType)98);
                Table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(EventEntity, Time)))
                    .ReturnsAsync(existingMessage)
                    .Verifiable();

                await InvokeMethod(
                    EventEntity,
                    Time,
                    type,
                    component,
                    status);

                Table.Verify();

                Table
                    .Verify(
                        x => x.InsertAsync(It.IsAny<MessageEntity>()),
                        Times.Never());

                Builder
                    .Verify(
                        x => x.Build(It.IsAny<MessageType>(), It.IsAny<IComponent>(), It.IsAny<ComponentStatus>()),
                        Times.Never());
            }

            [Fact]
            public async Task CreatesNewMessage()
            {
                var type = (MessageType)99;
                var status = (ComponentStatus)100;
                var componentStatus = (ComponentStatus)101;
                var component = new TestComponent("component")
                {
                    Status = componentStatus
                };

                var expectedStatus = GetExpectedStatus(component, status);

                var contents = "new message";
                Builder
                    .Setup(x => x.Build(type, component, expectedStatus))
                    .Returns(contents);
                
                Table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(EventEntity, Time)))
                    .ReturnsAsync((MessageEntity)null)
                    .Verifiable();

                Table
                    .Setup(x => x.InsertAsync(It.IsAny<MessageEntity>()))
                    .Callback<ITableEntity>(entity =>
                    {
                        var message = entity as MessageEntity;
                        Assert.NotNull(message);
                        Assert.Equal(EventEntity.RowKey, message.ParentRowKey);
                        Assert.Equal(Time, message.Time);
                        Assert.Equal(contents, message.Contents);
                    })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await InvokeMethod(
                    EventEntity,
                    Time,
                    type,
                    component,
                    status);

                Table.Verify();
            }

            protected abstract Task InvokeMethod(
                EventEntity eventEntity,
                DateTime time,
                MessageType type,
                IComponent component,
                ComponentStatus status);

            protected abstract ComponentStatus GetExpectedStatus(
                IComponent component,
                ComponentStatus status);
        }

        public class TheUpdateMessageAsyncMethod
            : MessageFactoryTest
        {
            [Fact]
            public async Task IgnoresIfMessageDoesNotExist()
            {
                var type = (MessageType)99;
                var component = new TestComponent("component");
                
                Table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(EventEntity, Time)))
                    .ReturnsAsync((MessageEntity)null);

                await Factory.UpdateMessageAsync(EventEntity, Time, type, component);

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<MessageEntity>()),
                        Times.Never());

                Builder
                    .Verify(
                        x => x.Build(It.IsAny<MessageType>(), It.IsAny<IComponent>(), It.IsAny<ComponentStatus>()),
                        Times.Never());
            }

            [Theory]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            [InlineData(MessageType.Manual)]
            public async Task IgnoresIfExistingMessageTypeDifferent(MessageType existingType)
            {
                var type = (MessageType)99;
                var component = new TestComponent("component");

                var existingMessage = new MessageEntity(EventEntity, Time, "existing", existingType);
                Table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(EventEntity, Time)))
                    .ReturnsAsync(existingMessage)
                    .Verifiable();

                await Factory.UpdateMessageAsync(EventEntity, Time, type, component);

                Table.Verify();

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<MessageEntity>()),
                        Times.Never());

                Builder
                    .Verify(
                        x => x.Build(It.IsAny<MessageType>(), It.IsAny<IComponent>(), It.IsAny<ComponentStatus>()),
                        Times.Never());
            }

            [Theory]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            [InlineData(MessageType.Manual)]
            public async Task ReplacesMessage(MessageType type)
            {
                var component = new TestComponent("component");

                var contents = "new message";
                Builder
                    .Setup(x => x.Build(type, component))
                    .Returns(contents);

                var message = new MessageEntity(EventEntity, Time, "existing", type);
                Table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(EventEntity, Time)))
                    .ReturnsAsync(message)
                    .Verifiable();

                Table
                    .Setup(x => x.ReplaceAsync(message))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Factory.UpdateMessageAsync(EventEntity, Time, type, component);

                Assert.Equal(EventEntity.RowKey, message.ParentRowKey);
                Assert.Equal(Time, message.Time);
                Assert.Equal(contents, message.Contents);

                Table.Verify();
            }
        }

        public class MessageFactoryTest
        {
            public static EventEntity EventEntity = new EventEntity() { RowKey = "rowKey" };
            public static DateTime Time = new DateTime(2018, 10, 10);

            public Mock<ITableWrapper> Table { get; }
            public Mock<IMessageContentBuilder> Builder { get; }

            public MessageFactory Factory { get; }

            public MessageFactoryTest()
            {
                Table = new Mock<ITableWrapper>();

                Builder = new Mock<IMessageContentBuilder>();

                Factory = new MessageFactory(
                    Table.Object,
                    Builder.Object,
                    Mock.Of<ILogger<MessageFactory>>());
            }
        }
    }
}
