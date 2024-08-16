// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "SymbolValidatorJob";
        private const string PackageNotFound = Prefix + "PackageNotFound";
        private const string SymbolsPackageNotFound = Prefix + "SymbolsPackageNotFound";
        private const string SymbolValidationDuration = Prefix + "SymbolValidationDurationInSeconds";
        private const string MessageDeliveryLag = Prefix + "MessageDeliveryLag";
        private const string MessageEnqueueLag = Prefix + "MessageEnqueueLag";
        private const string MessageHandlerDurationSeconds = Prefix + "MessageHandlerDurationSeconds";
        private const string MessageLockLost = Prefix + "MessageLockLost";
        private const string SymbolValidationResult = Prefix + "SymbolValidationResult";
        private const string SymbolAssemblyValidationResult = Prefix + "SymbolAssemblyValidationResult";

        private const string PackageId = "PackageId";
        private const string PackageNormalizedVersion = "PackageNormalizedVersion";
        private const string MessageType = "MessageType";
        private const string SymbolCount = "SymbolCount";
        private const string ValidationResult = "ValidationResult";
        private const string Issue = "Issue";
        private const string AssemblyName = "AssemblyName";
        private const string CallGuid = "CallGuid";
        private const string Handled = "Handled";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackPackageNotFoundEvent(string packageId, string packageNormalizedVersion)
        {
            _telemetryClient.TrackMetric(
                PackageNotFound,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion }
                });
        }

        public void TrackSymbolsPackageNotFoundEvent(string packageId, string packageNormalizedVersion)
        {
            _telemetryClient.TrackMetric(
                SymbolsPackageNotFound,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion }
                });
        }

        public IDisposable TrackSymbolValidationDurationEvent(string packageId, string packageNormalizedVersion, int symbolCount)
        {
            return _telemetryClient.TrackDuration(
                SymbolValidationDuration,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion },
                    { SymbolCount, symbolCount.ToString()}
                });
        }

        public void TrackSymbolsAssemblyValidationResultEvent(string packageId, string packageNormalizedVersion, ValidationStatus validationStatus, string issue, string assemblyName)
        {
            _telemetryClient.TrackMetric(
                SymbolAssemblyValidationResult,
                1,
                new Dictionary<string, string>
                {
                    { ValidationResult, validationStatus.ToString() },
                    { Issue, issue },
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion },
                    { AssemblyName, assemblyName }
                });
        }

        public void TrackSymbolsValidationResultEvent(string packageId, string packageNormalizedVersion, ValidationStatus validationStatus)
        {
            _telemetryClient.TrackMetric(
                SymbolValidationResult,
                1,
                new Dictionary<string, string>
                {
                    { ValidationResult, validationStatus.ToString() },
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion }
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

        public void TrackMessageHandlerDuration<TMessage>(TimeSpan duration, Guid callGuid, bool handled)
        {
            _telemetryClient.TrackMetric(
                MessageHandlerDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name },
                    { CallGuid, callGuid.ToString() },
                    { Handled, handled.ToString() }
                });
        }

        public void TrackMessageLockLost<TMessage>(Guid callGuid)
        {
            _telemetryClient.TrackMetric(
                MessageLockLost,
                1,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name },
                    { CallGuid, callGuid.ToString() }
                });
        }
    }
}
