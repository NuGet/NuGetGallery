// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status of a single validation step.
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// The validation step has not started yet.
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// The validation step is incomplete and should therefore be in progress.
        /// </summary>
        Incomplete = 1,

        /// <summary>
        /// The validation step has succeeded and no validation errors have occurred. Any transient errors that may have
        /// occurred during the validation step have been resolved.
        /// </summary>
        Succeeded = 2,

        /// <summary>
        /// The validation step has failed. This could be a failure in initiating the validation, the validation has timed
        /// out, or the logic of the validation has discovered an issue with the entity that is being validated.
        /// </summary>
        Failed = 3,
    }
}