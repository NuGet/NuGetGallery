// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.Telemetry
{
    public class TelemetryService : ITelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string OrchestratorPrefix = "Orchestrator.";
        private const string PackageSigningPrefix = "PackageSigning.";
        private const string PackageCertificatesPrefix = "PackageCertificates.";

        private const string DurationToValidationSetCreationSeconds = OrchestratorPrefix + "DurationToValidationSetCreationSeconds";
        private const string PackageStatusChange = OrchestratorPrefix + "PackageStatusChange";
        private const string TotalValidationDurationSeconds = OrchestratorPrefix + "TotalValidationDurationSeconds";
        private const string SentValidationTakingTooLongMessage = OrchestratorPrefix + "SentValidationTakingTooLongMessage";
        private const string ValidationSetTimeout = OrchestratorPrefix + "ValidationSetTimedOut";
        private const string ValidationIssue = OrchestratorPrefix + "ValidationIssue";
        private const string ValidationIssueCount = OrchestratorPrefix + "ValidationIssueCount";
        private const string ValidatorTimeout = OrchestratorPrefix + "ValidatorTimeout";
        private const string ValidatorDurationSeconds = OrchestratorPrefix + "ValidatorDurationSeconds";
        private const string ValidatorStarted = OrchestratorPrefix + "ValidatorStarted";
        private const string ClientValidationIssue = OrchestratorPrefix + "ClientValidationIssue";
        private const string MissingPackageForValidationMessage = OrchestratorPrefix + "MissingPackageForValidationMessage";
        private const string MissingNupkgForAvailablePackage = OrchestratorPrefix + "MissingNupkgForAvailablePackage";
        private const string DurationToHashPackageSeconds = OrchestratorPrefix + "DurationToHashPackageSeconds";
        private const string MessageDeliveryLag = OrchestratorPrefix + "MessageDeliveryLag";
        private const string MessageEnqueueLag = OrchestratorPrefix + "MessageEnqueueLag";
        private const string SymbolsMessageEnqueued = OrchestratorPrefix + "SymbolsMessageEnqueued";

        private const string DurationToStartPackageSigningValidatorSeconds = PackageSigningPrefix + "DurationToStartSeconds";

        private const string DurationToStartPackageCertificatesValidatorSeconds = PackageCertificatesPrefix + "DurationToStartSeconds";

        private const string FromStatus = "FromStatus";
        private const string ToStatus = "ToStatus";
        private const string IsSuccess = "IsSuccess";
        private const string ValidatorType = "ValidatorType";
        private const string IssueCode = "IssueCode";
        private const string ClientCode = "ClientCode";
        private const string PackageId = "PackageId";
        private const string NormalizedVersion = "NormalizedVersion";
        private const string ValidationTrackingId = "ValidationTrackingId";
        private const string PackageSize = "PackageSize";
        private const string HashAlgorithm = "HashAlgorithm";
        private const string StreamType = "StreamType";
        private const string MessageType = "MessageType";
        private const string ValidationId = "ValidationId";
        private const string OperationDateTime = "OperationDateTime";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public IDisposable TrackDurationToHashPackage(
            string packageId,
            string normalizedVersion,
            long packageSize,
            string hashAlgorithm,
            string streamType)
        {
            return _telemetryClient.TrackDuration(
                DurationToHashPackageSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { PackageSize, packageSize.ToString() },
                    { HashAlgorithm, hashAlgorithm },
                    { StreamType, streamType },
                });
        }

        public void TrackDurationToValidationSetCreation(TimeSpan duration)
        {
            _telemetryClient.TrackMetric(
                DurationToValidationSetCreationSeconds,
                duration.TotalSeconds);
        }

        public void TrackPackageStatusChange(PackageStatus fromStatus, PackageStatus toStatus)
        {
            _telemetryClient.TrackMetric(
                PackageStatusChange,
                1,
                new Dictionary<string, string>
                {
                    { FromStatus, fromStatus.ToString() },
                    { ToStatus, toStatus.ToString() },
                });
        }

        public void TrackTotalValidationDuration(TimeSpan duration, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                TotalValidationDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { IsSuccess, isSuccess.ToString() },
                });
        }

        public void TrackSentValidationTakingTooLongMessage(string packageId, string normalizedVersion, Guid validationTrackingId)
            => _telemetryClient.TrackMetric(
                    SentValidationTakingTooLongMessage,
                    1,
                    new Dictionary<string, string>
                    {
                        { PackageId, packageId },
                        { NormalizedVersion, normalizedVersion },
                        { ValidationTrackingId, validationTrackingId.ToString() },
                    });

        public void TrackValidationSetTimeout(string packageId, string normalizedVersion, Guid validationTrackingId)
            => _telemetryClient.TrackMetric(
                    ValidationSetTimeout,
                    1,
                    new Dictionary<string, string>
                    {
                        { PackageId, packageId },
                        { NormalizedVersion, normalizedVersion },
                        { ValidationTrackingId, validationTrackingId.ToString() },
                    });

        public void TrackValidationIssue(string validatorType, ValidationIssueCode code)
        {
            _telemetryClient.TrackMetric(
                ValidationIssue,
                1,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                    { IssueCode, code.ToString() },
                });
        }

        public void TrackValidationIssueCount(int count, string validatorType, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                ValidationIssueCount,
                count,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                    { IsSuccess, isSuccess.ToString() },
                });
        }

        public void TrackValidatorTimeout(string validatorType)
        {
            _telemetryClient.TrackMetric(
                ValidatorTimeout,
                1,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                });
        }

        public void TrackValidatorDuration(TimeSpan duration, string validatorType, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                ValidatorDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                    { IsSuccess, isSuccess.ToString() },
                });
        }

        public void TrackValidatorStarted(string validatorType)
        {
            _telemetryClient.TrackMetric(
                ValidatorStarted,
                1,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                });
        }

        public void TrackClientValidationIssue(string validatorType, string clientCode)
        {
            _telemetryClient.TrackMetric(
                ClientValidationIssue,
                1,
                new Dictionary<string, string>
                {
                    { ValidatorType, validatorType },
                    { ClientCode, clientCode },
                });
        }

        public void TrackMissingPackageForValidationMessage(string packageId, string normalizedVersion, string validationTrackingId)
            => _telemetryClient.TrackMetric(
                    MissingPackageForValidationMessage,
                    1,
                    new Dictionary<string, string>
                    {
                        { PackageId, packageId },
                        { NormalizedVersion, normalizedVersion },
                        { ValidationTrackingId, validationTrackingId },
                    });

        public void TrackMissingNupkgForAvailablePackage(string packageId, string normalizedVersion, string validationTrackingId)
            => _telemetryClient.TrackMetric(
                    MissingNupkgForAvailablePackage,
                    1,
                    new Dictionary<string, string>
                    {
                        { PackageId, packageId },
                        { NormalizedVersion, normalizedVersion },
                        { ValidationTrackingId, validationTrackingId },
                    });

        public IDisposable TrackDurationToStartPackageSigningValidator()
            => _telemetryClient.TrackDuration(DurationToStartPackageSigningValidatorSeconds);

        public IDisposable TrackDurationToStartPackageCertificatesValidator()
            => _telemetryClient.TrackDuration(DurationToStartPackageCertificatesValidatorSeconds);

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

        public void TrackSymbolsMessageEnqueued(string validatorName, Guid validationId)
           => _telemetryClient.TrackMetric(
               SymbolsMessageEnqueued,
               1,
               new Dictionary<string, string>
               {
                   { ValidatorType, validatorName },
                   { ValidationId, validationId.ToString()}
               });
    }
}
