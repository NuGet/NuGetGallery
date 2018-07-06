// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Signing;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.PackageSigning.Telemetry
{
    public class TelemetryService : ITelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string Prefix = "ProcessSignature.";
        private const string StrippedRepositorySignatures = Prefix + "StrippedRepositorySignatures";
        private const string DurationToStripRepositorySignaturesSeconds = Prefix + "DurationToStripRepositorySignaturesSeconds";
        private const string MessageDeliveryLag = Prefix + "MessageDeliveryLag";
        private const string MessageEnqueueLag = Prefix + "MessageEnqueueLag";

        private const string PackageId = "PackageId";
        private const string NormalizedVersion = "NormalizedVersion";
        private const string ValidationId = "ValidationId";
        private const string InputSignatureType = "InputSignatureType";
        private const string InputCounterSignatureCount = "InputCounterSignatureCount";
        private const string OutputSignatureType = "OutputSignatureType";
        private const string OutputCounterSignatureCount = "OutputCounterSignatureCount";
        private const string Changed = "Changed";
        private const string MessageType = "MessageType";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackStrippedRepositorySignatures(
            string packageId,
            string normalizedVersion,
            Guid validationId,
            PrimarySignature inputSignature,
            PrimarySignature outputSignature)
        {
            var properties = new Dictionary<string, string>
            {
                { PackageId, packageId },
                { NormalizedVersion, normalizedVersion },
                { ValidationId, validationId.ToString() },
                { InputSignatureType, inputSignature.Type.ToString() },
                { InputCounterSignatureCount, inputSignature.SignerInfo.CounterSignerInfos.Count.ToString() },
            };

            if (outputSignature != null)
            {
                properties.Add(OutputSignatureType, outputSignature.Type.ToString());
                properties.Add(OutputCounterSignatureCount, outputSignature.SignerInfo.CounterSignerInfos.Count.ToString());
            }

            _telemetryClient.TrackMetric(
                StrippedRepositorySignatures,
                1,
                properties);
        }

        public void TrackDurationToStripRepositorySignatures(
            TimeSpan duration,
            string packageId,
            string normalizedVersion,
            Guid validationId,
            bool changed)
        {
            var properties = new Dictionary<string, string>
            {
                { PackageId, packageId },
                { NormalizedVersion, normalizedVersion },
                { ValidationId, validationId.ToString() },
                { Changed, changed.ToString() },
            };

            _telemetryClient.TrackMetric(
                DurationToStripRepositorySignaturesSeconds,
                duration.TotalSeconds,
                properties);
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
