// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        public void TrackDurationToValidationSetCreation(TimeSpan duration)
        {
        }

        public void TrackPackageStatusChange(PackageStatus fromStatus, PackageStatus toStatus)
        {
        }

        public void TrackTotalValidationDuration(TimeSpan duration, bool isSuccess)
        {
        }

        public void TrackValidationIssue(string validatorType, ValidationIssueCode code)
        {
        }

        public void TrackValidationIssueCount(int count, string validatorType, bool isSuccess)
        {
        }

        public void TrackValidatorTimeout(string validatorType)
        {
        }

        public void TrackValidatorDuration(TimeSpan duration, string validatorType, bool isSuccess)
        {
        }

        public void TrackValidatorStarted(string validatorType)
        {
        }

        public void TrackClientValidationIssue(string validatorType, string clientCode)
        {
        }
    }
}
