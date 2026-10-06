// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class NamespaceReservationSubmissionResult
    {
        public const string SubmittedMessage = "Your request has been submitted successfully. It is being processed. Please check back later.";

        public IReadOnlyCollection<ValidationResult> Errors { get; }

        /// <summary>
        /// Submission outcome text. May include the model's reason; always HTML-encode on display.
        /// Does not contain internal rationale, policy references, or raw service/exception details.
        /// </summary>
        public string Message { get; }

        public bool IsWarning { get; }

        public NamespaceReservationSubmissionResult(IReadOnlyCollection<ValidationResult> errors, string message = null, bool isWarning = false)
        {
            Errors = errors;
            Message = message;
            IsWarning = isWarning;
        }
    }
}