// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    public class ValidationResult : IValidationResult
    {
        private static readonly IValidationResult notStarted = new ValidationResult(ValidationStatus.NotStarted);
        private static readonly IValidationResult incomplete = new ValidationResult(ValidationStatus.Incomplete);
        private static readonly IValidationResult succeeded = new ValidationResult(ValidationStatus.Succeeded);
        private static readonly IValidationResult failed = new ValidationResult(ValidationStatus.Failed);

        /// <summary>
        /// Represents a validation result that has not been started.
        /// </summary>
        public static IValidationResult NotStarted => notStarted;

        /// <summary>
        /// Represents a validation result that has started but not succeeded or failed yet.
        /// </summary>
        public static IValidationResult Incomplete => incomplete;

        /// <summary>
        /// A successful validation result.
        /// </summary>
        public static IValidationResult Succeeded => succeeded;

        /// <summary>
        /// A failed validation result with no errors.
        /// </summary>
        public static IValidationResult Failed => failed;

        /// <summary>
        /// Create a new validation result with the given status.
        /// </summary>
        /// <param name="status">The result's status.</param>
        public ValidationResult(ValidationStatus status)
            : this(status, null)
        {
        }

        /// <summary>
        /// Create a new failed validation result with the given errors.
        /// </summary>
        /// <param name="status">The status of the validation.</param>
        /// <param name="errors">If the validation has failed, the errors that detail why.</param>
        public ValidationResult(ValidationStatus status, IReadOnlyList<string> errors)
        {
            if (errors.Count > 0 && status != ValidationStatus.Failed)
            {
                throw new ArgumentException($"Cannot specify errors if the status is not '{ValidationStatus.Failed}'", nameof(status));
            }

            Status = status;
            Errors = errors ?? new string[0];
        }

        /// <summary>
        /// The status of the validation.
        /// </summary>
        public ValidationStatus Status { get; }

        /// <summary>
        /// The errors that were encountered if the validation failed.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Create a new failed <see cref="ValidationResult"/>.
        /// </summary>
        /// <param name="errors">The errors for the failed validation result.</param>
        /// <returns>The failed validation result.</returns>
        public static ValidationResult FailedWithErrors(params string[] errors)
        {
            return new ValidationResult(ValidationStatus.Failed, (string[])errors.Clone());
        }
    }
}
