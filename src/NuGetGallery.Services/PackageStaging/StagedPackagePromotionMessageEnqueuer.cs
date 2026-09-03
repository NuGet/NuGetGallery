// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;

namespace NuGetGallery
{
    /// <summary>
    /// Enqueues staged package promotion messages using Service Bus.
    /// </summary>
    public class StagedPackagePromotionMessageEnqueuer : IStagedPackagePromotionMessageEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<StagedPackagePromotionMessage> _serializer;

        public StagedPackagePromotionMessageEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<StagedPackagePromotionMessage> serializer)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc />
        public async Task SendMessageAsync(StagedPackagePromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var brokeredMessage = _serializer.Serialize(message);
            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}
