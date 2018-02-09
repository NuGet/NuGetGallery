// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public class SubscriptionProcessor<TMessage> : ISubscriptionProcessor<TMessage>
    {
        /// <summary>
        /// How quickly the shutdown task should check its status.
        /// </summary>
        private static readonly TimeSpan ShutdownPollTime = TimeSpan.FromSeconds(1);

        private readonly ISubscriptionClient _client;
        private readonly IBrokeredMessageSerializer<TMessage> _serializer;
        private readonly IMessageHandler<TMessage> _handler;
        private readonly ILogger<SubscriptionProcessor<TMessage>> _logger;

        private bool _running;
        private int _numberOfMessagesInProgress;

        public int NumberOfMessagesInProgress => _numberOfMessagesInProgress;

        /// <summary>
        /// Constructs a new subscription processor.
        /// </summary>
        /// <param name="client">The client used to receive messages from the subscription.</param>
        /// <param name="serializer">The serializer used to deserialize received messages.</param>
        /// <param name="handler">The handler used to handle received messages.</param>
        /// <param name="logger">The logger used to record debug information.</param>
        public SubscriptionProcessor(
            ISubscriptionClient client,
            IBrokeredMessageSerializer<TMessage> serializer,
            IMessageHandler<TMessage> handler,
            ILogger<SubscriptionProcessor<TMessage>> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _running = false;
            _numberOfMessagesInProgress = 0;
        }

        public void Start()
        {
            _logger.LogInformation("Registering the handler to begin listening to the Service Bus subscription");

            _running = true;

            _client.OnMessageAsync(OnMessageAsync, new OnMessageOptionsWrapper
            {
                AutoComplete = false,
            });
        }

        private async Task OnMessageAsync(IBrokeredMessage brokeredMessage)
        {
            if (!_running)
            {
                _logger.LogWarning("Dropping message from Service Bus as shutdown has been initiated");
                return;
            }

            Interlocked.Increment(ref _numberOfMessagesInProgress);

            try
            {
                using (var scope = _logger.BeginScope($"{nameof(SubscriptionProcessor<TMessage>)}.{nameof(OnMessageAsync)} {{CallGuid}} {{CallStartTimestamp}}",
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.ToString("O")))
                {
                    _logger.LogInformation("Received message from Service Bus subscription, processing");

                    var message = _serializer.Deserialize(brokeredMessage);

                    if (await _handler.HandleAsync(message))
                    {
                        _logger.LogInformation("Message was successfully handled, marking the brokered message as completed");

                        await brokeredMessage.CompleteAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Handler did not finish processing message, requeueing message to be reprocessed");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(Event.SubscriptionMessageHandlerException, e, "Requeueing message as it was unsuccessfully processed due to exception");
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _numberOfMessagesInProgress);
            }
        }

        public async Task<bool> ShutdownAsync(TimeSpan timeout)
        {
            // Wait until all in-flight messages complete, or, the maximum shutdown time is reached.
            var stopwatch = Stopwatch.StartNew();

            await StartShutdownAsync(timeout);

            while (_numberOfMessagesInProgress > 0)
            {
                await Task.Delay(ShutdownPollTime);

                _logger.LogInformation(
                    "{NumberOfMessagesInProgress} messages in progress after {TimeElapsed} seconds of graceful shutdown",
                    _numberOfMessagesInProgress,
                    stopwatch.Elapsed.TotalSeconds);

                if (stopwatch.Elapsed >= timeout)
                {
                    _logger.LogWarning(
                        "Forcefully shutting down even though there are {NumberOfMessagesInProgress} messages in progress",
                        _numberOfMessagesInProgress);

                    return false;
                }
            }

            return true;
        }

        private async Task StartShutdownAsync(TimeSpan timeout)
        {
            _logger.LogInformation(
                "Shutting down the subscription listener with {NumberOfMessagesInProgress} messages in progress",
                NumberOfMessagesInProgress);

            // Prevent "OnMessageAsync" from accepting more messages from Service Bus.
            _running = false;

            // Start two tasks: one for the shutdown and one for the shutdown's timeout. The first task to finish will
            // complete the "start shutdown" operation. Note that this method returns false if the timeout task completes first.
            var shutdownTask = _client.CloseAsync();
            var shutdownTimeoutTask = Task.Delay(timeout);

            var shutdownResult = await Task.WhenAny(shutdownTask, shutdownTimeoutTask);

            if (shutdownResult == shutdownTimeoutTask)
            {
                _logger.LogWarning(
                    "Timeout reached when starting shutdown of subscription processor after {StartShutdownTimeout}",
                    timeout);
            }
        }
    }
}
