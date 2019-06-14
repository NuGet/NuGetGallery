// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using NuGet.Services.ServiceBus;
using System;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteTelemetryService : IAccountDeleteTelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string Prefix = "AccountDeleter.";

        private readonly TelemetryClient _telemetryClient;

        public AccountDeleteTelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackAccountDelete()
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackEmailSent()
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackException(Exception exception)
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackIncomingCommand(AccountDeleteMessage command)
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool handled)
        {
            return;
            //throw new NotImplementedException();
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
            return;
            //throw new NotImplementedException();
        }
    }
}
