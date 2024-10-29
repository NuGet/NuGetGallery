// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A validation issue encountered during a validation step.
    /// </summary>
    public interface IValidationIssue
    {
        /// <summary>
        /// The code that classifies this issue.
        /// </summary>
        ValidationIssueCode IssueCode { get; }

        /// <summary>
        /// Serialize the contents of this validation issue.
        /// </summary>
        /// <returns>A string containing this error's serialized contents, excluding the error code.</returns>
        string Serialize();
    }
}