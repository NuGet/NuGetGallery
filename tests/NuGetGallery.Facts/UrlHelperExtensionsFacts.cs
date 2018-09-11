// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Web.Routing;
using Xunit;

namespace NuGetGallery
{
    public class UrlHelperExtensionsFacts
    {
        public class TheEnsureTrailingSlashHelperMethod
        {
            [Fact]
            public void Works()
            {
                string fixedUrl = UrlHelperExtensions.EnsureTrailingSlash("http://nuget.org/packages/FooPackage.CS");
                Assert.EndsWith("/", fixedUrl);
            }

            [Fact]
            public void PropagatesNull()
            {
                string fixedUrl = UrlHelperExtensions.EnsureTrailingSlash(null);
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

                string fixedUrl = UrlHelperExtensions.Package(TestUtility.MockUrlHelper(), package);

                Assert.DoesNotContain("metadata", fixedUrl);
                Assert.EndsWith(package.NormalizedVersion, fixedUrl);
            }

            [Theory]
            [InlineData("https://nuget.org", true, "/packages/id/1.0.0")]
            [InlineData("https://nuget.org", false, "https://nuget.org/packages/id/1.0.0")]
            [InlineData("https://localhost:66", true, "/packages/id/1.0.0")]
            [InlineData("https://localhost:66", false, "https://localhost:66/packages/id/1.0.0")]
            public void ReturnsCorrectRouteLink(string siteRoot, bool relativeUrl, string expectedUrl)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.SiteRoot = siteRoot;

                var urlHelper = TestUtility.MockUrlHelper(siteRoot);

                // Act
                var result = urlHelper.Package("id", "1.0.0", relativeUrl);

                // Assert
                Assert.Equal(expectedUrl, result);
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
                var absoluteReturnUrl = UrlHelperExtensions.GetAbsoluteReturnUrl(returnUrl, protocol, hostName);

                Assert.Equal(expectedReturnUrl, absoluteReturnUrl);
            }
        }

        public class TheGetActionLinkMethod : TestContainer
        {
            public static IEnumerable<object[]> GeneratesTheCorrectActionLink_Data
            {
                get
                {
                    var routeValueDictionary = new RouteValueDictionary();
                    routeValueDictionary["a"] = "b";

                    yield return new object[]
                    {
                        "https://nuget.org",
                        "ListPackages",
                        "Packages",
                        routeValueDictionary,
                        true,
                        "/packages?a=b"
                    };

                    yield return new object[]
                    {
                        "https://localhost:55",
                        "ListPackages",
                        "Packages",
                        routeValueDictionary,
                        true,
                        "/packages?a=b"
                    };

                    yield return new object[]
                    {
                        "https://nuget.org",
                        "ListPackages",
                        "Packages",
                        routeValueDictionary,
                        false,
                        "https://nuget.org/packages?a=b"
                    };

                    yield return new object[]
                    {
                        "https://localhost:55",
                        "ListPackages",
                        "Packages",
                        routeValueDictionary,
                        false,
                        "https://localhost:55/packages?a=b"
                    };
                }
            }

            [Theory]
            [MemberData(nameof(GeneratesTheCorrectActionLink_Data))]
            public void GeneratesTheCorrectActionLink(string siteRoot, string actionName, string controllerName, RouteValueDictionary routeValues, bool relativeUrl, string expectedActionLink)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.SiteRoot = siteRoot;

                var urlHelper = TestUtility.MockUrlHelper(siteRoot);

                // Act
                var result = UrlHelperExtensions.GetActionLink(urlHelper, actionName, controllerName, relativeUrl, routeValues);

                // Assert
                Assert.Equal(expectedActionLink, result);
            }
        }

        public class TheDeleteUserCertificateTemplateMethod : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                var urlHelper = TestUtility.MockUrlHelper();

                var result = urlHelper.DeleteUserCertificateTemplate().Resolve("thumbprint");

                Assert.Equal("/account/certificates/thumbprint", result);
            }
        }

        public class TheDeleteOrganizationCertificateTemplateMethod : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                var urlHelper = TestUtility.MockUrlHelper();

                var result = urlHelper.DeleteOrganizationCertificateTemplate("accountName").Resolve("thumbprint");

                Assert.Equal("/organization/accountName/certificates/thumbprint", result);
            }
        }

        public class TheSetRequiredSignerTemplateMethod : TestContainer
        {
            [Fact]
            public void ResolvePathIsCorrect()
            {
                var urlHelper = TestUtility.MockUrlHelper();
                var model = new StubPackageVersionModel();

                var result = urlHelper.SetRequiredSignerTemplate().Resolve(model);

                Assert.Equal("/packages/packageId/required-signer/{username}", result);
            }

            private sealed class StubPackageVersionModel : IPackageVersionModel
            {
                public string Id => "packageId";

                public string Version
                {
                    get => throw new NotImplementedException();
                    set => throw new NotImplementedException();
                }

                public string Title => throw new NotImplementedException();
            }
        }
    }
}
