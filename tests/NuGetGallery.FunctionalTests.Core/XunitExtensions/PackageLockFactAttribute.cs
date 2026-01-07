// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class PackageLockFactAttribute : FactAttribute
    {
        public PackageLockFactAttribute()
        {
            if (!GalleryConfiguration.Instance.TestPackageLock)
            {
                Skip = string.Format("Package locking shouldn't be tested");
            }
        }
    }
}
