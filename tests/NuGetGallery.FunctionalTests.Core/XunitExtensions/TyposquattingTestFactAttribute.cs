// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class TyposquattingTestFactAttribute : FactAttribute
    {
        public TyposquattingTestFactAttribute()
        {
            if (!GalleryConfiguration.Instance.TyposquattingCheckAndBlockUsers)
            {
                Skip = string.Format("Typosquatting checking and user blocking are disabled");
            }
        }
    }
}
