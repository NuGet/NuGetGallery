// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Measures promotion message latency and processing without per-promotion metric dimensions.
    /// </summary>
    public class StagingPromotionTelemetryService : ISubscriptionProcessorTelemetryService
    {
        private readonly ITelemetryClient _telemetryClient;

        public StagingPromotionTelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
        {
            _telemetryClient.TrackMetric("StagingPromotion.MessageDeliveryLagSeconds", deliveryLag.TotalSeconds, MessageProperties<TMessage>());
        }

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
        {
            _telemetryClient.TrackMetric("StagingPromotion.MessageEnqueueLagSeconds", enqueueLag.TotalSeconds, MessageProperties<TMessage>());
        }

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool handled)
        {
            var properties = MessageProperties<TMessage>();
            properties.Add("Handled", handled.ToString());
            _telemetryClient.TrackMetric("StagingPromotion.MessageHandlerDurationSeconds", duration.TotalSeconds, properties);
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
            _telemetryClient.TrackMetric("StagingPromotion.MessageLockLost", 1, MessageProperties<TMessage>());
        }

        private static Dictionary<string, string> MessageProperties<TMessage>()
        {
            return new Dictionary<string, string> { { "MessageType", typeof(TMessage).Name } };
        }
    }
}
