// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class NeedsManyVersionsFactAttribute : FactAttribute
    {
        public NeedsManyVersionsFactAttribute()
        {
            if (!GalleryConfiguration.Instance.HasManyVersions)
            {
                Skip = "This test requires packages with many versions (100+).";
            }
        }
    }
}
