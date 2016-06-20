// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Common.Validators
{
    public enum ValidationResult
    {
        /// <summary>
        /// Validation deadlettered. This value has to be negative.
        /// </summary>
        Deadlettered = -2,

        /// <summary>
        /// Validation failed. This value has to be negative.
        /// </summary>
        Failed = -1,

        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Validation succeeded.
        /// </summary>
        Succeeded = 1,

        /// <summary>
        /// Validation is dispatched on an asynchronous process that will report back on its own.
        /// </summary>
        Asynchronous = 2
    }
}