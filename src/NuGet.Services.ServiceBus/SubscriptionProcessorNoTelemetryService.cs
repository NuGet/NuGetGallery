// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// The implementation of the <see cref="ISubscriptionProcessorTelemetryService"/>
    /// that does not send any telemetry.
    /// </summary>
    public class SubscriptionProcessorNoTelemetryService : ISubscriptionProcessorTelemetryService
    {
        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
        {
        }

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
        {

        }

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool success)
        {
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
        }
    }
}
