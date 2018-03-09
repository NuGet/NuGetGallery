// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.Telemetry
{
    /// <summary>
    /// The interface used for emitting telemetry from the validation orchestrator.
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// The duration from when the package was created to when the first validation set was created. This metric
        /// is not emitted for revalidation requests.
        /// </summary>
        void TrackDurationToValidationSetCreation(TimeSpan duration);

        /// <summary>
        /// A counter metric emitted when a package changes package status. This metric is not emitted if package status
        /// does not change. This metric is emitted for revalidation if the terminal state changes.
        /// </summary>
        /// <param name="fromStatus">The status that the package moved from.</param>
        /// <param name="toStatus">The status that the package moved tp.</param>
        void TrackPackageStatusChange(PackageStatus fromStatus, PackageStatus toStatus);

        /// <summary>
        /// The total duration of all validators. This is the time that the validation set is first created until all of
        /// the validators have completed. This metric is also emitted for revalidations.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <param name="isSuccess">Whether or not all of the validations succeeded.</param>
        void TrackTotalValidationDuration(TimeSpan duration, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a validator fails due to the <see cref="ValidationConfigurationItem.FailAfter"/>
        /// configuration.
        /// </summary>
        /// <param name="validatorType">The validator type (name).</param>
        void TrackValidatorTimeout(string validatorType);

        /// <summary>
        /// The total duration of a single validator. This is the time from when the validation is first started until
        /// when the validation either completes or times out.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <param name="validatorType">The validator type (name).</param>
        /// <param name="isSuccess">Whether or not the validation succeeded.</param>
        void TrackValidatorDuration(TimeSpan duration, string validatorType, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a validator is started.
        /// </summary>
        /// <param name="validatorType">The validator type (name).</param>
        void TrackValidatorStarted(string validatorType);

        /// <summary>
        /// A counter metric emmitted when a validator reaches a terminal state and potentially persists validation
        /// issues. A count of zero is emitted if the validator does not produce any issues. This metric is not emitted
        /// if the validation is still at a non-terminal state.
        /// </summary>
        /// <param name="count">The number of issues.</param>
        /// <param name="validatorType">The validator type (name) that returned the issue list.</param>
        /// <param name="isSuccess">Whether or not the validation succeeded.</param>
        void TrackValidationIssueCount(int count, string validatorType, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a validation issue is created.
        /// </summary>
        /// <param name="validatorType">The validator type (name) the produced the issue.</param>
        /// <param name="code">The issue code.</param>
        void TrackValidationIssue(string validatorType, ValidationIssueCode code);

        /// <summary>
        /// A counter metric emitted when a client-mastered validation issue is emitted.
        /// </summary>
        /// <param name="validatorType">The validator type (name) the produced the issue.</param>
        /// <param name="clientCode">The client code.</param>
        void TrackClientValidationIssue(string validatorType, string clientCode);

        /// <summary>
        /// A counter metric emitted when the orchestrator is requested to validate a package,
        /// but, the package does not exist in the Gallery database even after <see cref="ValidationConfiguration.MissingPackageRetryCount"/>
        /// retries.
        /// </summary>
        void TrackMissingPackageForValidationMessage(string packageId, string normalizedVersion, string validationTrackingId);

        /// <summary>
        /// A metric for the case when orchestrator sees a package marked as available, but the blob is missing
        /// in the public container.
        /// </summary>
        void TrackMissingNupkgForAvailablePackage(string packageId, string normalizedVersion, string validationTrackingId);

        /// <summary>
        /// A metric to of how long it took to hash a package.
        /// </summary>
        void TrackDurationToHashPackage(TimeSpan duration, string packageId, string normalizedVersion, string hashAlgorithm, string streamType);
    }
}