// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Staging
{
    /// <summary>
    /// Serializes staging promotion messages for Service Bus.
    /// </summary>
    public class StagingPromotionMessageSerializer : IBrokeredMessageSerializer<StagingPromotionMessage>
    {
        private const string SchemaName = "ProcessStagingPromotion";

        private readonly IBrokeredMessageSerializer<StagingPromotionMessageData> _serializer
            = new BrokeredMessageSerializer<StagingPromotionMessageData>();

        /// <summary>
        /// Deserializes a staging promotion message.
        /// </summary>
        /// <param name="message">The received Service Bus message.</param>
        /// <returns>The staging promotion message.</returns>
        public StagingPromotionMessage Deserialize(IReceivedBrokeredMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var data = _serializer.Deserialize(message);
            switch (data.TargetType)
            {
                case StagingPromotionTargetType.StagingGroup:
                    return StagingPromotionMessage.ForGroup(data.PromotionId, data.TargetKey);
                case StagingPromotionTargetType.StagedPackage:
                    return StagingPromotionMessage.ForPackage(data.PromotionId, data.TargetKey);
                case StagingPromotionTargetType.StagedSymbolPackage:
                    return StagingPromotionMessage.ForSymbolPackage(data.PromotionId, data.TargetKey);
                default:
                    throw new InvalidOperationException($"Unknown staging promotion target type '{data.TargetType}'.");
            }
        }

        /// <summary>
        /// Serializes a staging promotion message.
        /// </summary>
        /// <param name="message">The staging promotion message.</param>
        /// <returns>The Service Bus message.</returns>
        public IBrokeredMessage Serialize(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return _serializer.Serialize(new StagingPromotionMessageData
            {
                PromotionId = message.PromotionId,
                TargetType = message.TargetType,
                TargetKey = message.TargetKey,
            });
        }

        [Schema(Name = SchemaName, Version = 1)]
        private class StagingPromotionMessageData
        {
            public Guid PromotionId { get; set; }

            public StagingPromotionTargetType TargetType { get; set; }

            public int TargetKey { get; set; }
        }

    }
}
