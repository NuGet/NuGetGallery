// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Entities
{
    public enum ContentScanType
    {
        /// <summary>
        /// A synchronous content scan. Scan results will be returned in the response to the scan submission.
        /// </summary>
        Sync = 0,

        /// <summary>
        /// An asynchronous content scan. The scan submission and scan result lookup are separate requests.
        /// </summary>
        BlockingAsync,

        /// <summary>
        /// An asynchronous content scan for passive CVS scan. The scan submission and scan result lookup are separate requests.
        /// </summary>
        NonBlockingAsync
    }
}