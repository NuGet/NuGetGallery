// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Framework;
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

        public class ThePackageHelperMethod
            : TestContainer
        {
            [Fact]
            public void UsesNormalizedVersionInUrls()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackageId"
                    },
                    NormalizedVersion = "1.0.0-alpha.1",
                    Version = "1.0.0-alpha.1+metadata"
                };
                
                string fixedUrl = UrlExtensions.Package(TestUtility.MockUrlHelper(), package);

                Assert.DoesNotContain("metadata", fixedUrl);
                Assert.EndsWith(package.NormalizedVersion, fixedUrl);
            }
        }

        public class TheGetAbsoluteReturnUrlMethod
        {
            [Theory]
            [InlineData("/", "https", "unittest.nuget.org", "https://unittest.nuget.org")]
            [InlineData("/Account/SignIn", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn")]
            [InlineData("https://localhost", "https", "unittest.nuget.org", "https://unittest.nuget.org")]
            [InlineData("https://localhost/Account/SignIn", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn")]
            [InlineData("https://localhost/Account/SignIn?returnUrl=%2F", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn?returnUrl=%2F")]
            public void UsesConfiguredSiteRootInAbsoluteUri(
                string returnUrl, 
                string protocol, 
                string hostName,
                string expectedReturnUrl)
            {
                var absoluteReturnUrl = UrlExtensions.GetAbsoluteReturnUrl(returnUrl, protocol, hostName);

                Assert.Equal(expectedReturnUrl, absoluteReturnUrl);
            }
        }
    }
}
