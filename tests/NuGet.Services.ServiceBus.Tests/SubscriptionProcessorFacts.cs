// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
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
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _serializer
                    .Setup(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()))
                    .Throws(new Exception());

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                await _target.StartAsync();

                var ex = await Record.ExceptionAsync(() => onMessageAsync(_brokeredMessage.Object));

                // Assert
                Assert.Null(ex);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Never);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackMessageHandlerDuration<TestMessage>(It.IsAny<TimeSpan>(), It.IsAny<Guid>(), false), Times.Once);
            }

            [Fact]
            public async Task CallsHandlerWhenMessageIsReceived()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Returns(Task.FromResult(true));

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                await _target.StartAsync();

                await onMessageAsync(_brokeredMessage.Object);

                // Assert
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Once);
                _telemetryService.Verify(t => t.TrackMessageHandlerDuration<TestMessage>(It.IsAny<TimeSpan>(), It.IsAny<Guid>(), true), Times.Once);
            }

            [Fact]
            public async Task DoesNotCompleteMessageIfHandlerReturnsFalse()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Returns(Task.FromResult(false));

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                await _target.StartAsync();

                await onMessageAsync(_brokeredMessage.Object);

                // Assert
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackMessageHandlerDuration<TestMessage>(It.IsAny<TimeSpan>(), It.IsAny<Guid>(), false), Times.Once);
            }

            [Fact]
            public async Task BrokedMessageIsntCompletedIfHandlerThrows()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Throws(new Exception());

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                await _target.StartAsync();

                var ex = await Record.ExceptionAsync(() => onMessageAsync(_brokeredMessage.Object));

                // Assert
                Assert.Null(ex);
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackMessageHandlerDuration<TestMessage>(It.IsAny<TimeSpan>(), It.IsAny<Guid>(), false), Times.Once);
            }

            [Fact]
            public async Task TracksFMessageLockLostExceptions()
            {
                // Arrange
                // Retrieve the OnMessageAsync callback that is registered to Service Bus's subscription client.
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                int? messagesInProgressDuringHandler = null;

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Callback(() => messagesInProgressDuringHandler = _target.NumberOfMessagesInProgress)
                    .Throws(new ServiceBusException("You snooze you lose", ServiceBusFailureReason.MessageLockLost));

                // Act
                // Start processing messages and trigger the OnMessageAsync callback.
                await _target.StartAsync();

                var ex = await Record.ExceptionAsync(() => onMessageAsync(_brokeredMessage.Object));

                // Assert
                Assert.Null(ex);
                Assert.Equal(1, messagesInProgressDuringHandler);
                Assert.Equal(0, _target.NumberOfMessagesInProgress);

                _serializer.Verify(s => s.Deserialize(It.IsAny<IReceivedBrokeredMessage>()), Times.Once);
                _handler.Verify(h => h.HandleAsync(It.IsAny<TestMessage>()), Times.Once);
                _brokeredMessage.Verify(m => m.CompleteAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackMessageLockLost<TestMessage>(It.IsAny<Guid>()), Times.Once);
                _telemetryService.Verify(t => t.TrackMessageHandlerDuration<TestMessage>(It.IsAny<TimeSpan>(), It.IsAny<Guid>(), false), Times.Once);
            }

            [Fact]
            public async Task TracksMessageLag()
            {
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);


                await _target.StartAsync();

                await onMessageAsync(_brokeredMessage.Object);

                _telemetryService
                    .Verify(ts => ts.TrackMessageDeliveryLag<TestMessage>(It.IsAny<TimeSpan>()), Times.Once);
                _telemetryService
                    .Verify(ts => ts.TrackEnqueueLag<TestMessage>(It.IsAny<TimeSpan>()), Times.Once);
            }

            [Fact]
            public async Task PassesMaxConcurrentCallsFurther()
            {
                IOnMessageOptions capturedOptions = null;
                _client
                    .Setup(c => c.StartProcessingAsync(It.IsAny<Func<IReceivedBrokeredMessage, Task>>(), It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((_, opt) => capturedOptions = opt);

                const int expectedConcurrentCalls = 42;
                await _target.StartAsync(expectedConcurrentCalls);

                _client
                    .Verify(c => c.StartProcessingAsync(It.IsAny<Func<IReceivedBrokeredMessage, Task>>(), It.IsAny<IOnMessageOptions>()), Times.Once);
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
                Func<IReceivedBrokeredMessage, Task> onMessageAsync = null;
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var brokeredMessage2 = new Mock<IReceivedBrokeredMessage>();

                _client
                    .Setup(c => c.StartProcessingAsync(
                                    It.IsAny<Func<IReceivedBrokeredMessage, Task>>(),
                                    It.IsAny<IOnMessageOptions>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Func<IReceivedBrokeredMessage, Task>, IOnMessageOptions>((callback, options) => onMessageAsync = callback);

                _handler
                    .Setup(h => h.HandleAsync(It.IsAny<TestMessage>()))
                    .Returns(Task.FromResult(true));

                // Act
                // Start processing messages, trigger the OnMessageAsync callback, stop processing messages, and trigger the OnMessageAsync callback again.
                await _target.StartAsync();

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

            protected readonly Mock<IReceivedBrokeredMessage> _brokeredMessage;

            public Base()
            {
                _client = new Mock<ISubscriptionClient>();
                _serializer = new Mock<IBrokeredMessageSerializer<TestMessage>>();
                _handler = new Mock<IMessageHandler<TestMessage>>();
                _telemetryService = new Mock<ISubscriptionProcessorTelemetryService>();

                _brokeredMessage = new Mock<IReceivedBrokeredMessage>();

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
