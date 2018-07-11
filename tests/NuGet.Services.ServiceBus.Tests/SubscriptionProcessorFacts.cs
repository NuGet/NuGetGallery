// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.ServiceBus.Tests
{
    public class SubscriptionProcessorFacts
    {
        public class TestMessage
        {
        }

        public class TheStartMethod : Base
        {
            [Fact]
            public async Task DoesntCallHandlerOnDeserializationException()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IBrokeredMessage, Task> onMessageAsync = null;

                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _serializer
                    .Setup(s => s.Deserialize(It.IsAny<IBrokeredMessage>()))
                    .Throws(new Exception());

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                _target.Start();

                var ex = await Record.ExceptionAsync(() => onMessageAsync(_brokeredMessage.Object));

                // Assert
                Assert.Null(ex);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Never);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
            }

            [Fact]
            public async Task CallsHandlerWhenMessageIsReceived()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Returns(Task.FromResult(true));

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                _target.Start();

                await onMessageAsync(_brokeredMessage.Object);

                // Assert
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotCompleteMessageIfHandlerReturnsFalse()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Returns(Task.FromResult(false));

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                _target.Start();

                await onMessageAsync(_brokeredMessage.Object);

                // Assert
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
            }

            [Fact]
            public async Task BrokedMessageIsntCompletedIfHandlerThrows()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Throws(new Exception());

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                _target.Start();

                var ex = await Record.ExceptionAsync(() => onMessageAsync(_brokeredMessage.Object));

                // Assert
                Assert.Null(ex);
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
            }

            [Fact]
            public async Task TracksMessageLag()
            {
                Func<IBrokeredMessage, Task> onMessageAsync = null;
                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);


                _target.Start();

                await onMessageAsync(_brokeredMessage.Object);

                _telemetryService
                    .Verify(ts => ts.TrackMessageDeliveryLag<TestMessage>(It.IsAny<TimeSpan>()), Times.Once);
                _telemetryService
                    .Verify(ts => ts.TrackEnqueueLag<TestMessage>(It.IsAny<TimeSpan>()), Times.Once);
            }

            [Fact]
            public void PassesMaxConcurrentCallsFurther()
            {
                IOnMessageOptions capturedOptions = null;
                _client
                    .Setup(c => c.OnMessageAsync(It.IsAny<Func<IBrokeredMessage, Task>>(), It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((_, opt) => capturedOptions = opt);

                const int expectedConcurrentCalls = 42;
                _target.Start(expectedConcurrentCalls);

                _client
                    .Verify(c => c.OnMessageAsync(It.IsAny<Func<IBrokeredMessage, Task>>(), It.IsAny<IOnMessageOptions>()), Times.Once);
                Assert.NotNull(capturedOptions);
                Assert.Equal(expectedConcurrentCalls, capturedOptions.MaxConcurrentCalls);
            }
        }

        public class TheShutdownAsyncMethod : Base
        {
            [Fact]
            public async Task ShutdownCallsTheClientsCloseAsyncMethod()
            {
                // Act
                await _target.ShutdownAsync(TimeSpan.FromDays(1));

                // Assert
                _client.Verify(c => c.CloseAsync(), Times.Once);
            }

            [Fact]
            public async Task ShutdownDropsNewMessages()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IBrokeredMessage, Task> onMessageAsync = null;
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var brokeredMessage2 = new Mock<IBrokeredMessage>();

                _client
                    .Setup(c => c.OnMessageAsync(
                                    It.IsAny<Func<IBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Callback<Func<IBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Returns(Task.FromResult(true));

                // Act
                // Start processing messages, trigger the OnMessageAsync callback, stop processing messages, and trigger the OnMessageAsync callback again.
                _target.Start();

                await onMessageAsync(_brokeredMessage.Object);

                var shutdownTask = _target.ShutdownAsync(TimeSpan.FromDays(1));

                await onMessageAsync(brokeredMessage2.Object);
                await shutdownTask;

                // Assert
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Once);
                brokeredMessage2.Verify(m => m.CompleteAsync(), Times.Never);
            }
        }

        public abstract class Base
        {
            protected readonly Mock<ISubscriptionClient> _client;
            protected readonly Mock<IBrokeredMessageSerializer<TestMessage>> _serializer;
            protected readonly Mock<IMessageHandler<TestMessage>> _handler;
            protected readonly Mock<ISubscriptionProcessorTelemetryService> _telemetryService;
            protected readonly SubscriptionProcessor<TestMessage> _target;

            protected readonly Mock<IBrokeredMessage> _brokeredMessage;

            public Base()
            {
                _client = new Mock<ISubscriptionClient>();
                _serializer = new Mock<IBrokeredMessageSerializer<TestMessage>>();
                _handler = new Mock<IMessageHandler<TestMessage>>();
                _telemetryService = new Mock<ISubscriptionProcessorTelemetryService>();

                _brokeredMessage = new Mock<IBrokeredMessage>();

                var logger = new Mock<ILogger<SubscriptionProcessor<TestMessage>>>();

                _target = new SubscriptionProcessor<TestMessage>(
                    _client.Object,
                    _serializer.Object,
                    _handler.Object,
                    _telemetryService.Object,
                    logger.Object);
            }
        }
    }
}
