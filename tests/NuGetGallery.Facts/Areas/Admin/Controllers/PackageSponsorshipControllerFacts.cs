// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using NuGetGallery.Services;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
	public class PackageSponsorshipControllerFacts
	{
		public class TheIndexMethod : FactsBase
		{
			[Fact]
			public void ReturnsViewModelWithoutPackageWhenNoPackageIdProvided()
			{
				// Act
				var result = Controller.Index();

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Null(model.PackageId);
				Assert.Null(model.Package);
				Assert.Null(model.SponsorshipUrls);
				Assert.Null(model.Message);
				Assert.False(model.IsSuccess);
			}

			[Fact]
			public void ReturnsViewModelWithPackageWhenValidPackageIdProvided()
			{
				// Arrange
				var packageId = "TestPackage";
				var packageRegistration = CreatePackageRegistration(packageId);
				var package = CreatePackage(packageRegistration, "1.0.0");
				var sponsorshipEntries = new List<SponsorshipUrlEntry>
				{
					new SponsorshipUrlEntry { Url = "https://github.com/sponsors/user", IsDomainAccepted = true }
				};

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(sponsorshipEntries);

				// Act
				var result = Controller.Index(packageId);

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.NotNull(model.Package);
				Assert.Equal(package.Id, model.Package.Id);
				Assert.NotNull(model.SponsorshipUrls);
				Assert.Single(model.SponsorshipUrls);
				Assert.Equal("https://github.com/sponsors/user", model.SponsorshipUrls.First().Url);
			}

			[Fact]
			public void ReturnsViewModelWithErrorWhenPackageNotFound()
			{
				// Arrange
				var packageId = "NonExistentPackage";
				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns((PackageRegistration)null);

				// Act
				var result = Controller.Index(packageId);

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.Null(model.Package);
				Assert.Contains("not found", model.Message);
				Assert.False(model.IsSuccess);
			}

			[Fact]
			public void ReturnsViewModelWithCustomMessage()
			{
				// Arrange
				var packageId = "TestPackage";
				var message = "Custom message";
				var isSuccess = true;

				// Act
				var result = Controller.Index(packageId, message, isSuccess);

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.Equal(message, model.Message);
				Assert.Equal(isSuccess, model.IsSuccess);
			}
		}

		public class TheAddUrlMethod : FactsBase
		{
			[Fact]
			public async Task RedirectsWithErrorWhenPackageNotFound()
			{
				// Arrange
				var packageId = "NonExistentPackage";
				var url = "https://github.com/sponsors/user";

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns((PackageRegistration)null);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Equal(packageId, redirectResult.RouteValues["packageId"]);
				Assert.Contains("not found", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenUserNotAuthenticated()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, null))
					.ThrowsAsync(new ArgumentNullException("user", "User cannot be null"));
				Controller.SetCurrentUser(null);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains("User cannot be null", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public async Task RedirectsWithErrorWhenUrlIsEmpty(string emptyUrl)
			{
				// Arrange
				var packageId = "TestPackage";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, emptyUrl, user))
					.ThrowsAsync(new ArgumentException("Please enter a URL."));
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.AddUrl(packageId, emptyUrl);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains("Please enter a URL", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenMaxLinksReached()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();
				var maxLinks = 5;

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks)
					.Returns(maxLinks);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
					.ThrowsAsync(new ArgumentException($"You can add a maximum of {maxLinks} sponsorship links."));
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains($"maximum of {maxLinks}", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithSuccessWhenUrlAddedSuccessfully()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());
				TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks)
					.Returns(10);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
					.ReturnsAsync(url);
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains("added successfully", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(true, redirectResult.RouteValues["isSuccess"]);

				SponsorshipUrlService.Verify(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user), Times.Once);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenArgumentExceptionThrown()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "invalid-url";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();
				var exceptionMessage = "Invalid URL format";

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());
				TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks)
					.Returns(10);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
					.ThrowsAsync(new ArgumentException(exceptionMessage));
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains($"Invalid URL: {exceptionMessage}", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenGeneralExceptionThrown()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();
				var exceptionMessage = "Database error";

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());
				TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks)
					.Returns(10);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
					.ThrowsAsync(new Exception(exceptionMessage));
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.AddUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains($"Error adding sponsorship URL: {exceptionMessage}", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}
		}

		public class TheRemoveUrlMethod : FactsBase
		{
			[Fact]
			public async Task RedirectsWithErrorWhenPackageNotFound()
			{
				// Arrange
				var packageId = "NonExistentPackage";
				var url = "https://github.com/sponsors/user";

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns((PackageRegistration)null);

				// Act
				var result = await Controller.RemoveUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Equal(packageId, redirectResult.RouteValues["packageId"]);
				Assert.Contains("not found", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenUserNotAuthenticated()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, null))
					.ThrowsAsync(new UnauthorizedAccessException("User cannot be null"));
				Controller.SetCurrentUser(null);

				// Act
				var result = await Controller.RemoveUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains("Error removing sponsorship URL: User cannot be null", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			[Fact]
			public async Task RedirectsWithSuccessWhenUrlRemovedSuccessfully()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, user))
					.Returns(Task.CompletedTask);
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.RemoveUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains("removed successfully", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(true, redirectResult.RouteValues["isSuccess"]);

				SponsorshipUrlService.Verify(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, user), Times.Once);
			}

			[Fact]
			public async Task RedirectsWithErrorWhenExceptionThrown()
			{
				// Arrange
				var packageId = "TestPackage";
				var url = "https://github.com/sponsors/user";
				var packageRegistration = CreatePackageRegistration(packageId);
				var user = CreateAdminUser();
				var exceptionMessage = "URL not found";

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, user))
					.ThrowsAsync(new InvalidOperationException(exceptionMessage));
				Controller.SetCurrentUser(user);

				// Act
				var result = await Controller.RemoveUrl(packageId, url);

				// Assert
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Contains($"Error removing sponsorship URL: {exceptionMessage}", redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}
		}

		public class FactsBase
		{
			public FactsBase()
			{
				PackageService = new Mock<IPackageService>();
				SponsorshipUrlService = new Mock<ISponsorshipUrlService>();
				TrustedSponsorshipDomains = new Mock<ITrustedSponsorshipDomains>();

				SponsorshipUrlService.Setup(x => x.TrustedSponsorshipDomains)
					.Returns(TrustedSponsorshipDomains.Object);

				Controller = new PackageSponsorshipController(
					PackageService.Object,
					SponsorshipUrlService.Object);

				HttpContext = new Mock<HttpContextBase>();
				TestUtility.SetupHttpContextMockForUrlGeneration(HttpContext, Controller);
				Controller.SetOwinContextOverride(Fakes.CreateOwinContext());
			}

			public Mock<IPackageService> PackageService { get; }
			public Mock<ISponsorshipUrlService> SponsorshipUrlService { get; }
			public Mock<ITrustedSponsorshipDomains> TrustedSponsorshipDomains { get; }
			public Mock<HttpContextBase> HttpContext { get; }
			public PackageSponsorshipController Controller { get; }

			protected static PackageRegistration CreatePackageRegistration(string id)
			{
				var packageRegistration = new PackageRegistration
				{
					Id = id,
					Key = 123,
					Packages = new List<Package>()
				};

				var package = CreatePackage(packageRegistration, "1.0.0");
				packageRegistration.Packages.Add(package);

				return packageRegistration;
			}

			protected static Package CreatePackage(PackageRegistration packageRegistration, string version)
			{
				return new Package
				{
					Key = 456,
					Id = packageRegistration.Id,
					NormalizedVersion = version,
					Version = version,
					PackageRegistration = packageRegistration
				};
			}

			protected static User CreateAdminUser()
			{
				return new User
				{
					Key = 789,
					Username = "admin",
					Roles = new List<Role>
					{
						new Role { Name = CoreConstants.AdminRoleName }
					}
				};
			}
		}
	}
}