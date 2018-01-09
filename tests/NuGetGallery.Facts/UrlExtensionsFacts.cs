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

        public class ThePackageRegistrationTemplateHelperMethod
            : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackageId"
                    },
                    Version = "1.0.0"
                };

                var urlHelper = TestUtility.MockUrlHelper();

                // Act
                var result = urlHelper.PackageRegistrationTemplate()
                    .Resolve(new ListPackageItemViewModel(package, currentUser: null));

                // Assert
                Assert.Equal(urlHelper.Package(package.PackageRegistration), result);
            }
        }

        public class TheEditPackageTemplateHelperMethod
            : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackageId"
                    },
                    Version = "1.0.0"
                };

                var urlHelper = TestUtility.MockUrlHelper();
                var packageVM = new ListPackageItemViewModel(package, currentUser: null);

                // Act
                var result = urlHelper.EditPackageTemplate().Resolve(packageVM);

                // Assert
                Assert.Equal(urlHelper.EditPackage(packageVM.Id, packageVM.Version), result);
            }
        }

        public class TheDeletePackageTemplateHelperMethod
            : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackageId"
                    },
                    Version = "1.0.0"
                };

                var urlHelper = TestUtility.MockUrlHelper();
                var packageVM = new ListPackageItemViewModel(package, currentUser: null);

                // Act
                var result = urlHelper.DeletePackageTemplate().Resolve(packageVM);

                // Assert
                Assert.Equal(urlHelper.DeletePackage(packageVM), result);
            }
        }

        public class TheManagePackageOwnersTemplateHelperMethod
            : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackageId"
                    },
                    Version = "1.0.0"
                };

                var urlHelper = TestUtility.MockUrlHelper();
                var packageVM = new ListPackageItemViewModel(package, currentUser: null);

                // Act
                var result = urlHelper.ManagePackageOwnersTemplate().Resolve(packageVM);

                // Assert
                Assert.Equal(urlHelper.ManagePackageOwners(packageVM), result);
            }
        }

        public class TheUserTemplateHelperMethod
            : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                // Arrange
                var user = new User("theUser");

                var urlHelper = TestUtility.MockUrlHelper();

                // Act
                var result = urlHelper.UserTemplate().Resolve(user);

                // Assert
                Assert.Equal(urlHelper.User(user), result);
            }
        }

        public class TheGetAbsoluteReturnUrlMethod
        {
            [Theory]
            [InlineData("/", "https", "unittest.nuget.org", "https://unittest.nuget.org")]
            [InlineData("/Account/SignIn", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn")]
            [InlineData("/Account/SignInNuGetAccount", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignInNuGetAccount")]
            [InlineData("https://localhost", "https", "unittest.nuget.org", "https://unittest.nuget.org")]
            [InlineData("https://localhost/Account/SignIn", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn")]
            [InlineData("https://localhost/Account/SignInNuGetAccount", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignInNuGetAccount")]
            [InlineData("https://localhost/Account/SignIn?returnUrl=%2F", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignIn?returnUrl=%2F")]
            [InlineData("https://localhost/Account/SignInNuGetAccount?returnUrl=%2F", "https", "unittest.nuget.org", "https://unittest.nuget.org/Account/SignInNuGetAccount?returnUrl=%2F")]
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
