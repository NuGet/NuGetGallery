// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Staging
{
    /// <summary>
    /// Enqueues staging promotion messages using Service Bus.
    /// </summary>
    public class StagingPromotionMessageEnqueuer : IStagingPromotionMessageEnqueuer
    {
        private readonly Lazy<ITopicClient> _topicClient;
        private readonly IBrokeredMessageSerializer<StagingPromotionMessage> _serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingPromotionMessageEnqueuer"/> class.
        /// </summary>
        /// <param name="topicClient">The Service Bus topic client.</param>
        /// <param name="serializer">The staging promotion message serializer.</param>
        public StagingPromotionMessageEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<StagingPromotionMessage> serializer)
            : this(new Lazy<ITopicClient>(() => topicClient), serializer)
        {
            if (topicClient == null)
            {
                throw new ArgumentNullException(nameof(topicClient));
            }
        }

        /// <summary>
        /// Initializes an enqueuer that creates its Service Bus client only when sending a promotion.
        /// </summary>
        /// <param name="topicClient">The deferred Service Bus topic client.</param>
        /// <param name="serializer">The staging promotion message serializer.</param>
        public StagingPromotionMessageEnqueuer(Lazy<ITopicClient> topicClient, IBrokeredMessageSerializer<StagingPromotionMessage> serializer)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc />
        public async Task SendMessageAsync(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            await _topicClient.Value.SendAsync(_serializer.Serialize(message));
        }
    }
}
