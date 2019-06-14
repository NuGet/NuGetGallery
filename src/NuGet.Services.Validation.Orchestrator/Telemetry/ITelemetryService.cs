// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

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
        void TrackDurationToValidationSetCreation(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration);

        /// <summary>
        /// Track how long a package's backup takes.
        /// </summary>
        /// <param name="validationSet">The validation set that requested the backup.</param>
        /// <returns>Reports the duration when disposed.</returns>
        IDisposable TrackDurationToBackupPackage(PackageValidationSet validationSet);

        /// <summary>
        /// A counter metric emitted when a package changes package status. This metric is not emitted if package status
        /// does not change. This metric is emitted for revalidation if the terminal state changes.
        /// </summary>
        /// <param name="packageId">Id of the package that changed status.</param>
        /// <param name="normalizedVersion">Normalized version of the package that changed status.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which change occurred.</param>
        /// <param name="fromStatus">The status that the package moved from.</param>
        /// <param name="toStatus">The status that the package moved to.</param>
        void TrackPackageStatusChange(string packageId, string normalizedVersion, Guid validationTrackingId, PackageStatus fromStatus, PackageStatus toStatus);

        /// <summary>
        /// The total duration of all validators. This is the time that the validation set is first created until all of
        /// the validators have completed. This metric is also emitted for revalidations.
        /// </summary>
        /// <param name="packageId">Id of the package which was being validated.</param>
        /// <param name="normalizedVersion">Normalized version of the package which was being validated.</param>
        /// <param name="validationTrackingId">Validation set tracking ID for which the duration was reported.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="isSuccess">Whether or not all of the validations succeeded.</param>
        void TrackTotalValidationDuration(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a notification is sent because a validation set takes too long.
        /// </summary>
        void TrackSentValidationTakingTooLongMessage(string packageId, string normalizedVersion, Guid validationTrackingId);

        /// <summary>
        /// A counter metric emitted when a validation set times out.
        /// </summary>
        void TrackValidationSetTimeout(string packageId, string normalizedVersion, Guid validationTrackingId);

        /// <summary>
        /// A counter metric emitted when a validation is past its validator's <see cref="ValidationConfigurationItem.TrackAfter"/>
        /// configuration.
        /// </summary>
        /// <param name="packageId">Id of the package for which validator is running too long.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which validator is running too long.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator that timed out runs.</param>
        /// <param name="validatorType">The validator type (name).</param>
        void TrackValidatorTimeout(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType);

        /// <summary>
        /// The total duration of a single validator. This is the time from when the validation is first started until
        /// when the validation either completes or times out.
        /// </summary>
        /// <param name="packageId">Id of the package which was being validated.</param>
        /// <param name="normalizedVersion">Normalized version of the package which was being validated.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator had run.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="validatorType">The validator type (name).</param>
        /// <param name="isSuccess">Whether or not the validation succeeded.</param>
        void TrackValidatorDuration(string packageId, string normalizedVersion, Guid validationTrackingId, TimeSpan duration, string validatorType, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a validator is started.
        /// </summary>
        /// <param name="packageId">Id of the package that is being validated.</param>
        /// <param name="normalizedVersion">Normalized version of the package that is being validated.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator runs.</param>
        /// <param name="validatorType">The validator type (name).</param>
        void TrackValidatorStarted(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType);

        /// <summary>
        /// The duration to start the package signing validator. This includes both the enqueue and DB commit time.
        /// </summary>
        IDisposable TrackDurationToStartPackageSigningValidator(string packageId, string normalizedVersion);

        /// <summary>
        /// The duration to start the package certificates validator. This includes all enqueue times and the DB commit
        /// time.
        /// </summary>
        IDisposable TrackDurationToStartPackageCertificatesValidator(string packageId, string normalizedVersion);

        /// <summary>
        /// A counter metric emmitted when a validator reaches a terminal state and potentially persists validation
        /// issues. A count of zero is emitted if the validator does not produce any issues. This metric is not emitted
        /// if the validation is still at a non-terminal state.
        /// </summary>
        /// <param name="packageId">Id of the package for which issues were reported.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which issues were reported.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator had run.</param>
        /// <param name="count">The number of issues.</param>
        /// <param name="validatorType">The validator type (name) that returned the issue list.</param>
        /// <param name="isSuccess">Whether or not the validation succeeded.</param>
        void TrackValidationIssueCount(string packageId, string normalizedVersion, Guid validationTrackingId, int count, string validatorType, bool isSuccess);

        /// <summary>
        /// A counter metric emitted when a validation issue is created.
        /// </summary>
        /// <param name="packageId">Id of the package for which issue is reported.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which issue is reported.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator had run.</param>
        /// <param name="validatorType">The validator type (name) the produced the issue.</param>
        /// <param name="code">The issue code.</param>
        void TrackValidationIssue(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType, ValidationIssueCode code);

        /// <summary>
        /// A counter metric emitted when a client-mastered validation issue is emitted.
        /// </summary>
        /// <param name="packageId">Id of the package for which issue is reported.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which issue is reported.</param>
        /// <param name="validationTrackingId">Validation set tracking ID in scope of which validator had run.</param>
        /// <param name="validatorType">The validator type (name) the produced the issue.</param>
        /// <param name="clientCode">The client code.</param>
        void TrackClientValidationIssue(string packageId, string normalizedVersion, Guid validationTrackingId, string validatorType, string clientCode);

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
        IDisposable TrackDurationToHashPackage(string packageId, string normalizedVersion, Guid validationTrackingId, long packageSize, string hashAlgorithm, string streamType);

        /// <summary>
        /// A metric to track the messages sent from Orchestrator to Validators and enqueued by validators. Ideally the messages should not duplicate.
        /// </summary>
        /// <param name="packageId">Id of the package for which message is sent.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which message is sent.</param>
        /// <param name="validatorName">The validator name the message was sent to.</param>
        /// <param name="validationId">The validationId.</param>
        void TrackSymbolsMessageEnqueued(string packageId, string normalizedVersion, string validatorName, Guid validationId);

        /// <summary>
        /// A metric to track duration of the license file extraction.
        /// </summary>
        /// <param name="packageId">Package ID from which license file is extracted.</param>
        /// <param name="normalizedVersion">Normalized version of the package from which license file is extracted.</param>
        /// <param name="validationTrackingId">Validation tracking ID for which extraction is done.</param>
        /// <returns></returns>
        IDisposable TrackDurationToExtractLicenseFile(string packageId, string normalizedVersion, string validationTrackingId);

        /// <summary>
        /// A metric to track duration of the license file deletion from flat container.
        /// </summary>
        /// <param name="packageId">Package ID for which licenes file is deleted.</param>
        /// <param name="normalizedVersion">Normalized version of the package for which license file is deleted.</param>
        /// <param name="validationTrackingId">Validation tracking ID for which delete operation is done.</param>
        IDisposable TrackDurationToDeleteLicenseFile(string packageId, string normalizedVersion, string validationTrackingId);
    }
}