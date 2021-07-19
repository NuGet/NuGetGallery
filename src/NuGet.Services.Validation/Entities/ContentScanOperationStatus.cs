// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Entities
{
    /// <summary>
    /// Cvs job scan status
    /// </summary>
    public enum ContentScanOperationStatus
    {
        /// <summary>
        /// Job is in pending state
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Submitted job is partially succeeded
        /// </summary>
        PartiallySucceeded,

        /// <summary>
        /// Job is successfully processed. 
        /// </summary>
        Succeeded,

        /// <summary>
        /// Submitted job is failed. 
        /// </summary>
        Failed
    }
}