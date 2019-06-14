// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator.Telemetry
{
    public class TelemetryService : ITelemetryService, ISubscriptionProcessorTelemetryService
    {
        private const string OrchestratorPrefix = "Orchestrator.";
        private const string PackageSigningPrefix = "PackageSigning.";
        private const string PackageCertificatesPrefix = "PackageCertificates.";

        private const string DurationToValidationSetCreationSeconds = OrchestratorPrefix + "DurationToValidationSetCreationSeconds";
        private const string DurationToBackupPackageSeconds = OrchestratorPrefix + "DurationToBackupPackageSeconds";
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
        private const string MessageHandlerDurationSeconds = OrchestratorPrefix + "MessageHandlerDurationSeconds";
        private const string MessageLockLost = OrchestratorPrefix + "MessageLockLost";
        private const string SymbolsMessageEnqueued = OrchestratorPrefix + "SymbolsMessageEnqueued";
        private const string ExtractLicenseFileDuration = OrchestratorPrefix + "ExtractLicenseFileDuration";
        private const string LicenseFileDeleted = OrchestratorPrefix + "LicenseFileDeleted";

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
        private const string CallGuid = "CallGuid";
        private const string Handled = "Handled";
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
            Guid validationTrackingId,
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
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { PackageSize, packageSize.ToString() },
                    { HashAlgorithm, hashAlgorithm },
                    { StreamType, streamType },
                });
        }

        public void TrackDurationToValidationSetCreation(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration)
        {
            _telemetryClient.TrackMetric(
                DurationToValidationSetCreationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                });
        }

        public IDisposable TrackDurationToBackupPackage(PackageValidationSet validationSet)
        {
            return _telemetryClient.TrackDuration(
                DurationToBackupPackageSeconds,
                new Dictionary<string, string>
                {
                    { ValidationTrackingId, validationSet.ValidationTrackingId.ToString() },
                    { PackageId, validationSet.PackageId },
                    { NormalizedVersion, validationSet.PackageNormalizedVersion }
                });
        }

        public void TrackPackageStatusChange(string packageId, string normalizedVersion, Guid validationTrackingId, PackageStatus fromStatus, PackageStatus toStatus)
        {
            _telemetryClient.TrackMetric(
                PackageStatusChange,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { FromStatus, fromStatus.ToString() },
                    { ToStatus, toStatus.ToString() },
                });
        }

        public void TrackTotalValidationDuration(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                TotalValidationDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
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

        public void TrackValidationIssue(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType, ValidationIssueCode code)
        {
            _telemetryClient.TrackMetric(
                ValidationIssue,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { ValidatorType, validatorType },
                    { IssueCode, code.ToString() },
                });
        }

        public void TrackValidationIssueCount(string packageId, string normalizedVersion, Guid validationTrackingId, int count, string validatorType, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                ValidationIssueCount,
                count,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { ValidatorType, validatorType },
                    { IsSuccess, isSuccess.ToString() },
                });
        }

        public void TrackValidatorTimeout(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType)
        {
            _telemetryClient.TrackMetric(
                ValidatorTimeout,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { ValidatorType, validatorType },
                });
        }

        public void TrackValidatorDuration(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration, string validatorType, bool isSuccess)
        {
            _telemetryClient.TrackMetric(
                ValidatorDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { ValidatorType, validatorType },
                    { IsSuccess, isSuccess.ToString() },
                });
        }

        public void TrackValidatorStarted(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType)
        {
            _telemetryClient.TrackMetric(
                ValidatorStarted,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
                    { ValidatorType, validatorType },
                });
        }

        public void TrackClientValidationIssue(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType, string clientCode)
        {
            _telemetryClient.TrackMetric(
                ClientValidationIssue,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId.ToString() },
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

        public IDisposable TrackDurationToStartPackageSigningValidator(string packageId, string normalizedVersion)
            => _telemetryClient.TrackDuration(
                DurationToStartPackageSigningValidatorSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                });

        public IDisposable TrackDurationToStartPackageCertificatesValidator(string packageId, string normalizedVersion)
            => _telemetryClient.TrackDuration(
                DurationToStartPackageCertificatesValidatorSeconds,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                });

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
            => _telemetryClient.TrackMetric(
                MessageDeliveryLag,
                deliveryLag.TotalSeconds,
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

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
            => _telemetryClient.TrackMetric(
                MessageEnqueueLag,
                enqueueLag.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name }
                });

        public void TrackSymbolsMessageEnqueued(string packageId, string normalizedVersion, string validatorName, Guid validationId)
            => _telemetryClient.TrackMetric(
                SymbolsMessageEnqueued,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidatorType, validatorName },
                    { ValidationId, validationId.ToString()}
                });

        public IDisposable TrackDurationToExtractLicenseFile(string packageId, string normalizedVersion, string validationTrackingId)
            => _telemetryClient.TrackDuration(ExtractLicenseFileDuration,
                new Dictionary<string, string> {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId },
                });

        public IDisposable TrackDurationToDeleteLicenseFile(string packageId, string normalizedVersion, string validationTrackingId)
            => _telemetryClient.TrackDuration(LicenseFileDeleted,
                new Dictionary<string, string> {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                    { ValidationTrackingId, validationTrackingId },
                });
    }
}
