// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

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
        private readonly ISubscriptionProcessorTelemetryService _telemetryService;
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
        /// <param name="telemetryService">The telemetry service reference to which this class submits telemetry.</param>
        /// <param name="logger">The logger used to record debug information.</param>
        public SubscriptionProcessor(
            ISubscriptionClient client,
            IBrokeredMessageSerializer<TMessage> serializer,
            IMessageHandler<TMessage> handler,
            ISubscriptionProcessorTelemetryService telemetryService,
            ILogger<SubscriptionProcessor<TMessage>> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _running = false;
            _numberOfMessagesInProgress = 0;
        }

        public async Task StartAsync()
        {
            await StartInternalAsync(new OnMessageOptionsWrapper
            {
                AutoComplete = false,
            });
        }

        public async Task StartAsync(int maxConcurrentCalls)
        {
            await StartInternalAsync(new OnMessageOptionsWrapper
            {
                AutoComplete = false,
                MaxConcurrentCalls = maxConcurrentCalls
            });
        }

        private async Task StartInternalAsync(OnMessageOptionsWrapper onMessageOptions)
        {
            _logger.LogInformation("Registering the handler to begin listening to the Service Bus subscription with options = {@OnMessageOptions}",
                onMessageOptions);

            _running = true;

            await _client.StartProcessingAsync(OnMessageAsync, onMessageOptions);
        }

        private async Task OnMessageAsync(IReceivedBrokeredMessage brokeredMessage)
        {
            if (!_running)
            {
                _logger.LogWarning("Dropping message from Service Bus as shutdown has been initiated");
                return;
            }

            Interlocked.Increment(ref _numberOfMessagesInProgress);

            TrackMessageLags(brokeredMessage);

            var callGuid = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            using (var scope = _logger.BeginScope($"{nameof(SubscriptionProcessor<TMessage>)}.{nameof(OnMessageAsync)} {{CallGuid}} {{CallStartTimestamp}} {{MessageId}}",
                callGuid,
                DateTimeOffset.UtcNow.ToString("O"),
                brokeredMessage.MessageId))
            {
                try
                {
                    _logger.LogInformation("Received message from Service Bus subscription, processing");

                    var message = _serializer.Deserialize(brokeredMessage);

                    if (await _handler.HandleAsync(message))
                    {
                        _logger.LogInformation(
                            "Message was successfully handled after {ElapsedSeconds} seconds, marking the brokered message as completed",
                            stopwatch.Elapsed.TotalSeconds);

                        await brokeredMessage.CompleteAsync();

                        _telemetryService.TrackMessageHandlerDuration<TMessage>(stopwatch.Elapsed, callGuid, handled: true);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Handler did not finish processing message after {DurationSeconds} seconds, requeueing message to be reprocessed",
                            stopwatch.Elapsed.TotalSeconds);

                        _telemetryService.TrackMessageHandlerDuration<TMessage>(stopwatch.Elapsed, callGuid, handled: false);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(
                        Event.SubscriptionMessageHandlerException,
                        e,
                        "Requeueing message as it was unsuccessfully processed due to exception after {DurationSeconds} seconds",
                        stopwatch.Elapsed.TotalSeconds);

                    if (e is ServiceBusException sbe && sbe.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        _telemetryService.TrackMessageLockLost<TMessage>(callGuid);
                    }

                    _telemetryService.TrackMessageHandlerDuration<TMessage>(stopwatch.Elapsed, callGuid, handled: false);

                    // exception should not be propagated to the topic client, because it will
                    // abandon the message and will cause the retry to happen immediately, which,
                    // in turn, have higher chances of failing again if we, for example, experiencing
                    // transitive network issues.
                }
                finally
                {
                    Interlocked.Decrement(ref _numberOfMessagesInProgress);
                }
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

        private void TrackMessageLags(IReceivedBrokeredMessage brokeredMessage)
        {
            _telemetryService.TrackMessageDeliveryLag<TMessage>(DateTimeOffset.UtcNow - brokeredMessage.ScheduledEnqueueTimeUtc);
            // we expect the "enqueue lag" to be zero or really close to zero pretty much all the time, logging it just in case it is not
            // and for historical perspective if we need one.
            _telemetryService.TrackEnqueueLag<TMessage>(brokeredMessage.EnqueuedTimeUtc - brokeredMessage.ScheduledEnqueueTimeUtc);
        }
    }
}
