// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface ITrustedImageDomains
    {
        /// <summary>
        /// Return true if input imageDomain is in trusted image allowlist, Otherwise, return false
        /// </summary>
        bool IsImageDomainTrusted(string imageDomain);
    }
}
