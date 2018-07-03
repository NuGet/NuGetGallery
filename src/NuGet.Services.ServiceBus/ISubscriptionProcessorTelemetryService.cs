// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// Interface for the telemetry emitted by <see cref="SubscriptionProcessor{TMessage}"/>.
    /// </summary>
    public interface ISubscriptionProcessorTelemetryService
    {
        /// <summary>
        /// Tracks the time difference between the time a message was received and the time
        /// it was scheduled to be enqueued.
        /// </summary>
        /// <remarks>
        /// Scheduled enqueueing set by <see cref="IBrokeredMessage.ScheduledEnqueueTimeUtc"/> 
        /// affects the reported time: if message was enqueued by the code at time T, had
        /// scheduled delivery time set to T+S and was actually received at T+S+L, the reported
        /// value is going to be: (T+S+L) - (T+S) = L.
        /// </remarks>
        void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag);

        /// <summary>
        /// Tracks the time difference between the scheduled enqueue time and actual enqueue time.
        /// </summary>
        /// <remarks>
        /// This time being noticeably greater than zero might indicate perf issues on Service Bus
        /// side, if we ever to see any.
        /// </remarks>
        void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag);
    }
}
