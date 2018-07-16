// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class TelemetryService : ITelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string Prefix = "ValidateCertificate.";
        private const string PackageSignatureMayBeInvalidated = Prefix + "PackageSignatureMayBeInvalidated";
        private const string PackageSignatureShouldBeInvalidated = Prefix + "PackageSignatureShouldBeInvalidated";
        private const string UnableToValidateCertificate = Prefix + "UnableToValidateCertificate";
        private const string MessageDeliveryLag = Prefix + "MessageDeliveryLag";
        private const string MessageEnqueueLag = Prefix + "MessageEnqueueLag";

        private const string PackageId = "PackageId";
        private const string PackageNormalizedVersion = "PackageNormalizedVersion";
        private const string PackageSignatureId = "PackageSignatureId";
        private const string CertificateId = "CertificateId";
        private const string CertificateThumbprint = "CertificateThumbprint";
        private const string MessageType = "MessageType";

        private readonly TelemetryClient _telemetryClient;

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackPackageSignatureMayBeInvalidatedEvent(PackageSignature signature)
        {
            _telemetryClient.TrackMetric(
                PackageSignatureMayBeInvalidated,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, signature.PackageSigningState.PackageId },
                    { PackageNormalizedVersion, signature.PackageSigningState.PackageNormalizedVersion },
                    { PackageSignatureId, signature.Key.ToString() }
                });
        }

        public void TrackPackageSignatureShouldBeInvalidatedEvent(PackageSignature signature)
        {
            _telemetryClient.TrackMetric(
                PackageSignatureShouldBeInvalidated,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, signature.PackageSigningState.PackageId },
                    { PackageNormalizedVersion, signature.PackageSigningState.PackageNormalizedVersion },
                    { PackageSignatureId, signature.Key.ToString() }
                });
        }

        public void TrackUnableToValidateCertificateEvent(EndCertificate certificate)
        {
            _telemetryClient.TrackMetric(
                PackageSignatureMayBeInvalidated,
                1,
                new Dictionary<string, string>
                {
                    { CertificateId, certificate.Key.ToString() },
                    { CertificateThumbprint, certificate.Thumbprint }
                });
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
            => _telemetryClient.TrackMetric(
                MessageDeliveryLag,
                deliveryLag.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name }
                });

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
            => _telemetryClient.TrackMetric(
                MessageEnqueueLag,
                enqueueLag.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name }
                });
    }
}
