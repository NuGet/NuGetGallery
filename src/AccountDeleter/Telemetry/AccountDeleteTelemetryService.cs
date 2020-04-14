// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using NuGet.Services.ServiceBus;
using System;
using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteTelemetryService : IAccountDeleteTelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string TelemetryPrefix = "AccountDeleter.";

        private const string DeleteResultEventName = TelemetryPrefix + "DeleteResult";
        private const string EmailSentEventName = TelemetryPrefix + "EmailSent";
        private const string EmailBlockedEventName = TelemetryPrefix + "EmailBlocked";
        private const string UnknownSourceEventName = TelemetryPrefix + "UnknownSource";
        private const string UserNotFoundEventName = TelemetryPrefix + "UserNotFound";
        private const string UnconfirmedUserEventName = TelemetryPrefix + "UnconfirmedUser";

        private const string CallGuidDimensionName = "CallGuid";
        private const string ContactAllowedDimensionName = "ContactAllowed";
        private const string DeleteResultSuccessDimensionName = "Succeeded";
        private const string HandledDimensionName = "Handled";
        private const string MessageTypeDimensionName = "MessageType";
        private const string SourceDimensionName = "Source";

        private const string MessageDeliveryLagMetricName = TelemetryPrefix + "MessageDeliveryLag";
        private const string MessageEnqueueLagMetricName = TelemetryPrefix + "MessageEnqueueLag";
        private const string MessageHandlerDurationSecondsMetricName = TelemetryPrefix + "MessageHandlerDurationSeconds";
        private const string MessageLockLostMetricName = TelemetryPrefix + "MessageLockLost";

        private readonly TelemetryClient _telemetryClient;

        public AccountDeleteTelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackUserNotFound(string source)
        {
            _telemetryClient.TrackEvent(UserNotFoundEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source }
                });
        }

        public void TrackDeleteResult(string source, bool deleteSuccess)
        {
            _telemetryClient.TrackEvent(DeleteResultEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source },
                    { DeleteResultSuccessDimensionName, deleteSuccess.ToString() }
                });
        }

        public void TrackEmailSent(string source, bool contactAllowed)
        {
            _telemetryClient.TrackEvent(EmailSentEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source },
                    { ContactAllowedDimensionName, contactAllowed.ToString() }
                }) ;
        }

        public void TrackEmailBlocked(string source)
        {
            _telemetryClient.TrackEvent(EmailBlockedEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source }
                });
        }

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
        {
            _telemetryClient.TrackMetric(
                   MessageEnqueueLagMetricName,
                   enqueueLag.TotalSeconds,
                   new Dictionary<string, string>
                   {
                    { MessageTypeDimensionName, typeof(TMessage).Name }
                   });
        }

        public void TrackUnknownSource(string source)
        {
            _telemetryClient.TrackEvent(UnknownSourceEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source }
                });
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
        {
            _telemetryClient.TrackMetric(
                   MessageDeliveryLagMetricName,
                   deliveryLag.TotalSeconds,
                   new Dictionary<string, string>
                   {
                    { MessageTypeDimensionName, typeof(TMessage).Name }
                   });
        }

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool handled)
        {
            _telemetryClient.TrackMetric(
                   MessageHandlerDurationSecondsMetricName,
                   duration.TotalSeconds,
                   new Dictionary<string, string>
                   {
                    { MessageTypeDimensionName, typeof(TMessage).Name },
                    { CallGuidDimensionName, callGuid.ToString() },
                    { HandledDimensionName, handled.ToString() }
                   });
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
            _telemetryClient.TrackMetric(
                   MessageLockLostMetricName,
                   1,
                   new Dictionary<string, string>
                   {
                    { MessageTypeDimensionName, typeof(TMessage).Name },
                    { CallGuidDimensionName, callGuid.ToString() }
                   });
        }

        public void TrackUnconfirmedUser(string source)
        {
            _telemetryClient.TrackEvent(UnconfirmedUserEventName,
                new Dictionary<string, string>
                {
                    { SourceDimensionName, source }
                });
        }
    }
}
