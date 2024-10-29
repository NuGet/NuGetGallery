// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Messaging
{
    public class EmailMessageEnqueuer : IEmailMessageEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IServiceBusMessageSerializer _serializer;
        private readonly ILogger<EmailMessageEnqueuer> _logger;

        public EmailMessageEnqueuer(
            ITopicClient topicClient,
            IServiceBusMessageSerializer serializer,
            ILogger<EmailMessageEnqueuer> logger)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendEmailMessageAsync(EmailMessageData message)
        {
            _logger.LogTrace(
                "Serializing EmailMessageData with tracking id {MessageTrackingId}.",
                message.MessageTrackingId);

            var brokeredMessage = _serializer.SerializeEmailMessageData(message);

            _logger.LogTrace(
                "Successfully serialized EmailMessageData with tracking id {MessageTrackingId}.",
                message.MessageTrackingId);

            _logger.LogInformation(
                "Enqueuing EmailMessageData with tracking id {MessageTrackingId}.",
                message.MessageTrackingId);

            await _topicClient.SendAsync(brokeredMessage);

            _logger.LogInformation(
                "Successfully enqueued EmailMessageData with tracking id {MessageTrackingId}.",
                message.MessageTrackingId);
        }
    }
}
