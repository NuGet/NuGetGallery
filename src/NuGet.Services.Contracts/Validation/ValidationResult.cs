// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The result of an asynchronous validation.
    /// </summary>
    public interface IValidationResult
    {
        /// <summary>
        /// The status of the validation.
        /// </summary>
        ValidationStatus Status { get; }

        /// <summary>
        /// The errors that were encountered if the validation failed.
        /// </summary>
        IReadOnlyList<string> Errors { get; }
    }
}
