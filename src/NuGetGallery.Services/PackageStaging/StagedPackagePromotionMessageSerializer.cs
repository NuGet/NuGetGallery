// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGetGallery
{
    /// <summary>
    /// Serializes staged package promotion messages for Service Bus.
    /// </summary>
    public class StagedPackagePromotionMessageSerializer : IBrokeredMessageSerializer<StagedPackagePromotionMessage>
    {
        private const string SchemaName = "ProcessStagedPackagePromotion";

        private readonly IBrokeredMessageSerializer<StagedPackagePromotionMessageData> _serializer
            = new BrokeredMessageSerializer<StagedPackagePromotionMessageData>();

        /// <summary>
        /// Deserializes a staged package promotion message.
        /// </summary>
        /// <param name="message">The received Service Bus message.</param>
        /// <returns>The staged package promotion message.</returns>
        public StagedPackagePromotionMessage Deserialize(IReceivedBrokeredMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var data = _serializer.Deserialize(message);
            return new StagedPackagePromotionMessage(data.PromotionId, data.StagedPackageKey);
        }

        /// <summary>
        /// Serializes a staged package promotion message.
        /// </summary>
        /// <param name="message">The staged package promotion message.</param>
        /// <returns>The Service Bus message.</returns>
        public IBrokeredMessage Serialize(StagedPackagePromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return _serializer.Serialize(new StagedPackagePromotionMessageData
            {
                PromotionId = message.PromotionId,
                StagedPackageKey = message.StagedPackageKey,
            });
        }

        [Schema(Name = SchemaName, Version = 1)]
        private class StagedPackagePromotionMessageData
        {
            public Guid PromotionId { get; set; }

            public int StagedPackageKey { get; set; }
        }
    }
}
