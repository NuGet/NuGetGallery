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
        /// A successful validation result with no issues.
        /// </summary>
        public static IValidationResult Succeeded => succeeded;

        /// <summary>
        /// A failed validation result with no issues.
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
        /// <param name="issues">The issues that were encountered during the validation. Must be empty if
        /// status is not <see cref="ValidationStatus.Failed"/> or <see cref="Validation.ValidationStatus.Succeeded"/></param>
        public ValidationResult(ValidationStatus status, IReadOnlyList<IValidationIssue> issues)
        {
            if (issues?.Count > 0 && status != ValidationStatus.Succeeded && status != ValidationStatus.Failed)
            {
                throw new ArgumentException($"Cannot specify issues if the validation is not in a terminal status'", nameof(status));
            }

            Status = status;
            Issues = issues ?? new IValidationIssue[0];
        }

        /// <summary>
        /// The status of the validation.
        /// </summary>
        public ValidationStatus Status { get; }

        /// <summary>
        /// The issues that were encountered during the validation.
        /// </summary>
        public IReadOnlyList<IValidationIssue> Issues { get; }

        /// <summary>
        /// Create a new failed <see cref="ValidationResult"/>.
        /// </summary>
        /// <param name="issues">The issues for the failed validation result.</param>
        /// <returns>The failed validation result.</returns>
        public static ValidationResult FailedWithIssues(params IValidationIssue[] issues)
        {
            return new ValidationResult(ValidationStatus.Failed, (IValidationIssue[])issues.Clone());
        }
    }
}