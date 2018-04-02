// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Logging;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryClient _client;

        private const string Prefix = "RevalidateCertificate.";

        private const string PromoteSignatureDuration = Prefix + "PromoteSignatureDuration";
        private const string CertificateRevalidationDuration = Prefix + "CertificateRevalidationDuration";
        private const string CertificateRevalidationTakingTooLong = Prefix + "CertificateRevalidationTakingTooLong";
        private const string CertificateRevalidationReachedTimeout = Prefix + "CertificateRevalidationReachedTimeout";

        public TelemetryService(ITelemetryClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IDisposable TrackPromoteSignaturesDuration()
            =>_client.TrackDuration(PromoteSignatureDuration);

        public IDisposable TrackCertificateRevalidationDuration()
            => _client.TrackDuration(CertificateRevalidationDuration);

        public void TrackCertificateRevalidationTakingTooLong()
            => _client.TrackMetric(CertificateRevalidationTakingTooLong, 1);

        public void TrackCertificateRevalidationReachedTimeout()
            => _client.TrackMetric(CertificateRevalidationReachedTimeout, 1);
    }
}
