// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Frameworks;
using NuGetGallery.Helpers;
using NuGetGallery.Services;
using Xunit;

namespace NuGetGallery
{
	public class ManagePackageViewModelFactorySponsorshipFacts
	{
		public class TheConstructor
		{
			[Fact]
			public void ThrowsArgumentNullExceptionWhenSponsorshipUrlServiceIsNull()
			{
				// Act & Assert
				Assert.Throws<ArgumentNullException>(() => new ManagePackageViewModelFactory(
					Mock.Of<IIconUrlProvider>(),
					Mock.Of<IPackageFrameworkCompatibilityFactory>(),
					Mock.Of<IFeatureFlagService>(),
					null));
			}
		}

		public class TheSponsorshipUrlIntegration : FactsBase
		{
			[Fact]
			public void PopulatesEmptySponsorshipUrlsWhenNoneExist()
			{
				// Arrange
				var package = CreatePackage();
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				var url = CreateUrlHelper();

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(package.PackageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());

				// Act
				var result = Factory.Create(package, user, reasons, url, null, false, false);

				// Assert
				Assert.NotNull(result.SponsorshipUrlEntries);
				Assert.Empty(result.SponsorshipUrlEntries);
			}

			[Fact]
			public void HandlesNullSponsorshipUrlEntriesGracefully()
			{
				// Arrange
				var package = CreatePackage();
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				var url = CreateUrlHelper();

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(package.PackageRegistration))
					.Returns((List<SponsorshipUrlEntry>)null);

				// Act
				var result = Factory.Create(package, user, reasons, url, null, false, false);

				// Assert
				Assert.NotNull(result.SponsorshipUrlEntries);
				Assert.Empty(result.SponsorshipUrlEntries);
			}

			[Fact]
			public void PopulatesSponsorshipUrlsWithMixedDomainAcceptance()
			{
				// Arrange
				var package = CreatePackage();
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				var url = CreateUrlHelper();

				var sponsorshipEntries = new List<SponsorshipUrlEntry>
				{
					new SponsorshipUrlEntry { Url = "https://github.com/sponsors/validuser", IsDomainAccepted = true },
					new SponsorshipUrlEntry { Url = "https://suspicious-site.com/sponsor", IsDomainAccepted = false },
					new SponsorshipUrlEntry { Url = "https://ko-fi.com/validuser", IsDomainAccepted = true },
					new SponsorshipUrlEntry { Url = "https://another-untrusted.com/pay", IsDomainAccepted = false }
				};

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(package.PackageRegistration))
					.Returns(sponsorshipEntries);

				// Act
				var result = Factory.Create(package, user, reasons, url, null, false, false);

				// Assert
				Assert.NotNull(result.SponsorshipUrlEntries);
				Assert.Equal(4, result.SponsorshipUrlEntries.Count);

				// Verify accepted domains
				var acceptedEntries = result.SponsorshipUrlEntries.Where(x => x.IsDomainAccepted).ToList();
				Assert.Equal(2, acceptedEntries.Count);
				Assert.Contains(acceptedEntries, x => x.Url == "https://github.com/sponsors/validuser");
				Assert.Contains(acceptedEntries, x => x.Url == "https://ko-fi.com/validuser");

				// Verify rejected domains
				var rejectedEntries = result.SponsorshipUrlEntries.Where(x => !x.IsDomainAccepted).ToList();
				Assert.Equal(2, rejectedEntries.Count);
				Assert.Contains(rejectedEntries, x => x.Url == "https://suspicious-site.com/sponsor");
				Assert.Contains(rejectedEntries, x => x.Url == "https://another-untrusted.com/pay");
			}

			[Fact]
			public void SponsorshipUrlServiceIsCalledWithCorrectPackageRegistration()
			{
				// Arrange
				var packageRegistration = CreatePackageRegistration("MyTestPackage");
				var package = CreatePackage(packageRegistration);
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				var url = CreateUrlHelper();

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(It.IsAny<PackageRegistration>()))
					.Returns(new List<SponsorshipUrlEntry>());

				// Act
				Factory.Create(package, user, reasons, url, null, false, false);

				// Assert
				SponsorshipUrlService.Verify(x => x.GetSponsorshipUrlEntries(packageRegistration), Times.Once);
			}

			[Fact]
			public void SponsorshipUrlEntriesArePreservedInSetupMethod()
			{
				// Arrange
				var existingViewModel = new ManagePackageViewModel();
				var package = CreatePackage();
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				var url = CreateUrlHelper();

				var sponsorshipEntries = new List<SponsorshipUrlEntry>
				{
					new SponsorshipUrlEntry { Url = "https://github.com/sponsors/user", IsDomainAccepted = true }
				};

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(package.PackageRegistration))
					.Returns(sponsorshipEntries);

				// Act
				var result = Factory.Setup(existingViewModel, package, user, reasons, url, null, false, false);

				// Assert
				Assert.Same(existingViewModel, result);
				Assert.NotNull(result.SponsorshipUrlEntries);
				Assert.Single(result.SponsorshipUrlEntries);
				Assert.Equal("https://github.com/sponsors/user", result.SponsorshipUrlEntries.First().Url);
				Assert.True(result.SponsorshipUrlEntries.First().IsDomainAccepted);
			}

			[Fact]
			public void HandlesSponsorshipUrlServiceException()
			{
				// Arrange
				var package = CreatePackage();
				var user = CreateUser();
				var reasons = new List<ReportPackageReason>();
				
				// Set up configuration service for URL helper extensions
				var mockConfig = new Mock<IGalleryConfigurationService>();
				var mockConfigCurrent = new Mock<IAppConfiguration>();
				mockConfig.SetupGet(c => c.Current).Returns(mockConfigCurrent.Object);
				mockConfigCurrent.SetupGet(c => c.RequireSSL).Returns(false);
				mockConfig.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost");
				UrlHelperExtensions.SetConfigurationService(mockConfig.Object);
				
				// Create a working URL helper setup specifically for this test
				var mockHttpContext = new Mock<HttpContextBase>();
				var mockHttpRequest = new Mock<HttpRequestBase>();
				mockHttpContext.Setup(c => c.Request).Returns(mockHttpRequest.Object);
				mockHttpRequest.Setup(r => r.Url).Returns(new Uri("https://localhost/"));
				mockHttpRequest.Setup(r => r.ApplicationPath).Returns("/");
				mockHttpRequest.Setup(r => r.IsSecureConnection).Returns(true);
				mockHttpRequest.Setup(r => r.ServerVariables).Returns(new System.Collections.Specialized.NameValueCollection());
				
				var mockHttpResponse = new Mock<HttpResponseBase>();
				mockHttpContext.Setup(c => c.Response).Returns(mockHttpResponse.Object);
				mockHttpResponse.Setup(r => r.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
				
				var requestContext = new RequestContext(mockHttpContext.Object, new RouteData());
				var routes = new RouteCollection();
				Routes.RegisterRoutes(routes);
				var url = new UrlHelper(requestContext, routes);

				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(package.PackageRegistration))
					.Throws(new InvalidOperationException("Service error"));

				// Act & Assert
				var exception = Assert.Throws<InvalidOperationException>(() => 
					Factory.Create(package, user, reasons, url, null, false, false));
				
				Assert.Equal("Service error", exception.Message);
			}
		}

		public class FactsBase
		{
			public FactsBase()
			{
				IconUrlProvider = new Mock<IIconUrlProvider>();
				PackageFrameworkCompatibilityFactory = new Mock<IPackageFrameworkCompatibilityFactory>();
				FeatureFlagService = new Mock<IFeatureFlagService>();
				SponsorshipUrlService = new Mock<ISponsorshipUrlService>();

				Factory = new ManagePackageViewModelFactory(
					IconUrlProvider.Object,
					PackageFrameworkCompatibilityFactory.Object,
					FeatureFlagService.Object,
					SponsorshipUrlService.Object);
			}

			public Mock<IIconUrlProvider> IconUrlProvider { get; }
			public Mock<IPackageFrameworkCompatibilityFactory> PackageFrameworkCompatibilityFactory { get; }
			public Mock<IFeatureFlagService> FeatureFlagService { get; }
			public Mock<ISponsorshipUrlService> SponsorshipUrlService { get; }
			public ManagePackageViewModelFactory Factory { get; }

			protected static PackageRegistration CreatePackageRegistration(string id = "TestPackage")
			{
				return new PackageRegistration
				{
					Id = id,
					Key = 123,
					Packages = new List<Package>()
				};
			}

			protected static Package CreatePackage(PackageRegistration packageRegistration = null, string version = "1.0.0")
			{
				packageRegistration = packageRegistration ?? CreatePackageRegistration();

				var package = new Package
				{
					Key = 456,
					Version = version,
					PackageRegistration = packageRegistration,
					PackageStatusKey = PackageStatus.Available,
					Listed = true,
					Dependencies = new List<PackageDependency>()
				};

				if (packageRegistration.Packages == null)
				{
					packageRegistration.Packages = new List<Package>();
				}

				if (!packageRegistration.Packages.Contains(package))
				{
					packageRegistration.Packages.Add(package);
				}

				return package;
			}

			protected static User CreateUser(string username = "testuser")
			{
				return new User
				{
					Key = 789,
					Username = username,
					Roles = new List<Role>()
				};
			}

			protected static UrlHelper CreateUrlHelper()
			{
				return TestUtility.MockUrlHelper();
			}
		}
	}
}
