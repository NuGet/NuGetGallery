// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "Orchestrator.";

        private const string DurationToValidationSetCreationSeconds = Prefix + "DurationToValidationSetCreationSeconds";
        private const string PackageStatusChange = Prefix + "PackageStatusChange";
        private const string TotalValidationDurationSeconds = Prefix + "TotalValidationDurationSeconds";
        private const string ValidationIssue = Prefix + "ValidationIssue";
        private const string ValidationIssueCount = Prefix + "ValidationIssueCount";
        private const string ValidatorTimeout = Prefix + "ValidatorTimeout";
        private const string ValidatorDurationSeconds = Prefix + "ValidatorDurationSeconds";
        private const string ValidatorStarted = Prefix + "ValidatorStarted";
        private const string ClientValidationIssue = Prefix + "ClientValidationIssue";

        private const string FromStatus = "FromStatus";
        private const string ToStatus = "ToStatus";
        private const string IsSuccess = "IsSuccess";
        private const string ValidatorType = "ValidatorType";
        private const string IssueCode = "IssueCode";
        private const string ClientCode = "ClientCode";

        private readonly TelemetryClient _telemetryClient;

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
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
    }
}
