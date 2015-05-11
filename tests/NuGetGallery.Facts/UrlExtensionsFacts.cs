// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Xunit;

namespace NuGetGallery
{
    public class UrlExtensionsFacts
    {
        public class TheEnsureTrailingSlashHelperMethod
        {
            [Fact]
            public void Works()
            {
                string fixedUrl = UrlExtensions.EnsureTrailingSlash("http://nuget.org/packages/FooPackage.CS");
                Assert.True(fixedUrl.EndsWith("/", StringComparison.Ordinal));
            }

            [Fact]
            public void PropagatesNull()
            {
                string fixedUrl = UrlExtensions.EnsureTrailingSlash(null);
                Assert.Null(fixedUrl);
            }
        }
    }
}
