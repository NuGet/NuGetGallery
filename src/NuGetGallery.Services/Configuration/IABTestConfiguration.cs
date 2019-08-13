// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface IABTestConfiguration
    {
        /// <summary>
        /// A value between 0 and 100 (inclusive) representing the desired percentage of users the should get the preview search
        /// experience.
        /// </summary>
        int PreviewSearchPercentage { get; }

        /// <summary>
        /// A value between 0 and 100 (inclusive) representing the desired percentage of users the should get the preview
        /// hijack experience.
        /// </summary>
        int PreviewHijackPercentage { get; }
    }
}