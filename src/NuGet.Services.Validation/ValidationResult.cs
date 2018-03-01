// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    public class ValidationResult : IValidationResult
    {
        /// <summary>
        /// Represents a validation result that has not been started.
        /// </summary>
        public static IValidationResult NotStarted { get; } = new ValidationResult(ValidationStatus.NotStarted);

        /// <summary>
        /// Represents a validation result that has started but not succeeded or failed yet.
        /// </summary>
        public static IValidationResult Incomplete { get; } = new ValidationResult(ValidationStatus.Incomplete);

        /// <summary>
        /// A successful validation result with no issues.
        /// </summary>
        public static IValidationResult Succeeded { get; } = new ValidationResult(ValidationStatus.Succeeded);

        /// <summary>
        /// A failed validation result with no issues.
        /// </summary>
        public static IValidationResult Failed { get; } = new ValidationResult(ValidationStatus.Failed);

        /// <summary>
        /// Create a new validation result with the given status.
        /// </summary>
        /// <param name="status">The result's status.</param>
        public ValidationResult(ValidationStatus status)
            : this(status, issues: null, nupkgUrl: null)
        {
        }

        /// <summary>
        /// Create a new validation result with the given status.
        /// </summary>
        /// <param name="status">The result's status.</param>
        /// <param name="nupkgUrl">
        /// A URL to modified package content (.nupkg). Must be null if status is not 
        /// <see cref="ValidationStatus.Succeeded"/>.
        /// </param>
        public ValidationResult(
            ValidationStatus status,
            string nupkgUrl)
            : this(status, issues: null, nupkgUrl: nupkgUrl)
        {
        }

        /// <summary>
        /// Create a new validation result with the given status.
        /// </summary>
        /// <param name="status">The result's status.</param>
        /// <param name="issues">
        /// The issues that were encountered during the validation. Must be empty if status is not
        /// <see cref="ValidationStatus.Failed"/> or <see cref="Validation.ValidationStatus.Succeeded"/>.
        /// </param>
        public ValidationResult(
            ValidationStatus status,
            IReadOnlyList<IValidationIssue> issues)
            : this(status, issues, nupkgUrl: null)
        {
        }

        /// <summary>
        /// Create a new failed validation result with the given errors.
        /// </summary>
        /// <param name="status">The status of the validation.</param>
        /// <param name="issues">
        /// The issues that were encountered during the validation. Must be empty if status is not
        /// <see cref="ValidationStatus.Failed"/> or <see cref="Validation.ValidationStatus.Succeeded"/>.
        /// </param>
        /// <param name="nupkgUrl">
        /// A URL to modified package content (.nupkg). Must be null if status is not 
        /// <see cref="ValidationStatus.Succeeded"/>.
        /// </param>
        public ValidationResult(
            ValidationStatus status,
            IReadOnlyList<IValidationIssue> issues,
            string nupkgUrl)
        {
            if (issues?.Count > 0 && status != ValidationStatus.Succeeded && status != ValidationStatus.Failed)
            {
                throw new ArgumentException("Cannot specify issues if the validation is not in a terminal status.", nameof(status));
            }

            if (nupkgUrl != null && status != ValidationStatus.Succeeded)
            {
                throw new ArgumentException($"The {nameof(nupkgUrl)} can only be provided when the status is " +
                    $"{ValidationStatus.Succeeded}.", nameof(status));
            }

            Status = status;
            Issues = issues ?? new IValidationIssue[0];
            NupkgUrl = nupkgUrl;
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
        /// The URL to the modified NuGet package content. This URL should be accessible without special authentication
        /// headers. However, authentication information could be included in the URL (e.g. Azure Blob Storage SAS URL).
        /// This URL need not have a single value for a specific <see cref="ValidationId"/>.
        /// </summary>
        public string NupkgUrl { get; }

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