// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    public class NuGetValidationResponse : INuGetValidationResponse
    {
        /// <summary>
        /// Represents a validation step that has not been started.
        /// </summary>
        public static INuGetValidationResponse NotStarted { get; } = new NuGetValidationResponse(ValidationStatus.NotStarted);

        /// <summary>
        /// Represents a validation step that has started but not succeeded or failed yet.
        /// </summary>
        public static INuGetValidationResponse Incomplete { get; } = new NuGetValidationResponse(ValidationStatus.Incomplete);

        /// <summary>
        /// A successful validation step with no issues.
        /// </summary>
        public static INuGetValidationResponse Succeeded { get; } = new NuGetValidationResponse(ValidationStatus.Succeeded);

        /// <summary>
        /// A failed validation step with no issues.
        /// </summary>
        public static INuGetValidationResponse Failed { get; } = new NuGetValidationResponse(ValidationStatus.Failed);

        /// <summary>
        /// Create a new validation step response with the given status.
        /// </summary>
        /// <param name="status">The step's status.</param>
        public NuGetValidationResponse(ValidationStatus status)
            : this(status, issues: null, nupkgUrl: null)
        {
        }

        /// <summary>
        /// Create a new validation step response with the given status.
        /// </summary>
        /// <param name="status">The step's status.</param>
        /// <param name="nupkgUrl">
        /// A URL to modified package content (.nupkg). Must be null if status is not 
        /// <see cref="ValidationStatus.Succeeded"/>.
        /// </param>
        public NuGetValidationResponse(
            ValidationStatus status,
            string nupkgUrl)
            : this(status, issues: null, nupkgUrl: nupkgUrl)
        {
        }

        /// <summary>
        /// Create a new validation step repsonse with the given status.
        /// </summary>
        /// <param name="status">The step's status.</param>
        /// <param name="issues">
        /// The issues that were encountered during the validation. Must be empty if status is not
        /// <see cref="ValidationStatus.Failed"/> or <see cref="Validation.ValidationStatus.Succeeded"/>.
        /// </param>
        public NuGetValidationResponse(
            ValidationStatus status,
            IReadOnlyList<IValidationIssue> issues)
            : this(status, issues, nupkgUrl: null)
        {
        }

        /// <summary>
        /// Create a new failed validation step response with the given errors.
        /// </summary>
        /// <param name="status">The status of the validation step.</param>
        /// <param name="issues">
        /// The issues that were encountered during the validation step. Must be empty if status is not
        /// <see cref="ValidationStatus.Failed"/> or <see cref="Validation.ValidationStatus.Succeeded"/>.
        /// </param>
        /// <param name="nupkgUrl">
        /// A URL to modified package content (.nupkg). Must be null if status is not 
        /// <see cref="ValidationStatus.Succeeded"/>.
        /// </param>
        public NuGetValidationResponse(
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
        /// The status of the validation step.
        /// </summary>
        public ValidationStatus Status { get; }

        /// <summary>
        /// The issues that were encountered during the validation step.
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
        /// <param name="issues">The issues for the failed validation step response.</param>
        /// <returns>The failed validation step response.</returns>
        public static NuGetValidationResponse FailedWithIssues(params IValidationIssue[] issues)
        {
            return new NuGetValidationResponse(ValidationStatus.Failed, (IValidationIssue[])issues.Clone());
        }
    }
}