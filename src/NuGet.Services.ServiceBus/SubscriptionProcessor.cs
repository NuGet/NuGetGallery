// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public class SubscriptionProcessor<TMessage> : ISubscriptionProcessor<TMessage>
    {
        private readonly ISubscriptionClient _client;
        private readonly IBrokeredMessageSerializer<TMessage> _serializer;
        private readonly IMessageHandler<TMessage> _handler;
        private readonly ILogger<SubscriptionProcessor<TMessage>> _logger;

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

            _numberOfMessagesInProgress = 0;
        }

        public void Start()
        {
            _logger.LogInformation("Registering the handler to begin listening to the Service Bus subscription");

            _client.OnMessageAsync(OnMessageAsync, new OnMessageOptionsWrapper
            {
                AutoComplete = false,
            });
        }

        private async Task OnMessageAsync(IBrokeredMessage brokeredMessage)
        {
            Interlocked.Increment(ref _numberOfMessagesInProgress);

            try
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
            catch (Exception e)
            {
                _logger.LogError("Requeueing message as it was unsuccessfully processed due to exception: {Exception}", e);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _numberOfMessagesInProgress);
            }
        }

        public async Task StartShutdownAsync()
        {
            _logger.LogInformation(
                "Shutting down the subscription listener with {NumberOfMessagesInProgress} messages in progress",
                NumberOfMessagesInProgress);

            await _client.CloseAsync();
        }
    }
}
