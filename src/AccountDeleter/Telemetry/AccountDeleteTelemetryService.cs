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
            throw new NotImplementedException();
        }

        public void TrackEmailSent()
        {
            throw new NotImplementedException();
        }

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
        {
            throw new NotImplementedException();
        }

        public void TrackException(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void TrackIncomingCommand(AccountDeleteMessage command)
        {
            throw new NotImplementedException();
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
        {
            throw new NotImplementedException();
        }

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool handled)
        {
            throw new NotImplementedException();
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
            throw new NotImplementedException();
        }
    }
}
