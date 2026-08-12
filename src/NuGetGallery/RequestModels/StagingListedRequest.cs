// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class StagingListedRequest
    {
        /// <summary>
        /// The desired listed intent. This is nullable so the controller can distinguish an explicitly supplied value
        /// from an absent field on a <c>PATCH</c> request and reject the latter rather than defaulting to <c>false</c>.
        /// </summary>
        public bool? Listed { get; set; }
    }
}
