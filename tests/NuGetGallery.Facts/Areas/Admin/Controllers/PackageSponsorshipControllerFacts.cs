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
				AssertIndexViewModel(result, packageId: null, hasPackage: false, hasMessage: false, isSuccess: false);
			}

			[Fact]
			public void ReturnsViewModelWithPackageWhenValidPackageIdProvided()
			{
				// Arrange
				var packageId = "TestPackage";
				var packageRegistration = CreatePackageRegistration(packageId);
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
				Assert.NotNull(model.SponsorshipUrls);
				Assert.Single(model.SponsorshipUrls);
				Assert.Equal("https://github.com/sponsors/user", model.SponsorshipUrls.First().Url);
			}

			[Fact]
			public void ReturnsViewModelWithErrorWhenPackageNotFound()
			{
				// Arrange
				var packageId = "NonExistentPackage";
				SetupPackageNotFound(packageId);

				// Act
				var result = Controller.Index(packageId);

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.Null(model.Package);
				Assert.Equal($"Package '{packageId}' not found.", model.Message);
				Assert.False(model.IsSuccess);
			}

			[Fact]
			public void ReturnsViewModelWithCustomMessageWhenPackageFound()
			{
				// Arrange
				var packageId = "TestPackage";
				var message = "Custom message";
				var isSuccess = true;
				var packageRegistration = CreatePackageRegistration(packageId);

				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());

				// Act
				var result = Controller.Index(packageId, message, isSuccess);

				// Assert
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.Equal(message, model.Message);
				Assert.Equal(isSuccess, model.IsSuccess);
				Assert.NotNull(model.Package);
			}
		}

		public class TheAddUrlMethod : FactsBase
		{
			public class ErrorScenarios : FactsBase
			{
				[Fact]
				public async Task RedirectsWithErrorWhenPackageNotFound()
				{
					// Arrange
					var packageId = "NonExistentPackage";
					var url = TestUrls.ValidGitHubUrl;
					SetupPackageNotFound(packageId);

					// Act
					var result = await Controller.AddUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, "not found");
				}

				[Fact]
				public async Task RedirectsWithErrorWhenUserNotAuthenticated()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
					var packageRegistration = CreatePackageRegistration(packageId);

					PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
						.Returns(packageRegistration);
					SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, null))
						.ThrowsAsync(new ArgumentNullException("user", "User cannot be null"));
					Controller.SetCurrentUser(null);

					// Act
					var result = await Controller.AddUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, "User cannot be null");
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

					SetupValidPackageAndUser(packageRegistration, user);
					SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, emptyUrl, user))
						.ThrowsAsync(new SponsorshipUrlValidationException("Please enter a URL."));

					// Act
					var result = await Controller.AddUrl(packageId, emptyUrl);

					// Assert
					AssertRedirectWithError(result, packageId, "Please enter a URL");
				}

				[Fact]
				public async Task RedirectsWithErrorWhenMaxLinksReached()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
					var packageRegistration = CreatePackageRegistration(packageId);
					var user = CreateAdminUser();
					var maxLinks = 5;

					SetupValidPackageAndUser(packageRegistration, user);
					TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks).Returns(maxLinks);
					SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
						.ThrowsAsync(new SponsorshipUrlValidationException($"You can add a maximum of {maxLinks} sponsorship links."));

					// Act
					var result = await Controller.AddUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, $"maximum of {maxLinks}");
				}

				[Theory]
				[InlineData("invalid-url", "Invalid URL format")]
				[InlineData("ftp://example.com", "Unsupported protocol")]
				public async Task RedirectsWithErrorWhenSponsorshipUrlValidationExceptionThrown(string invalidUrl, string expectedErrorMessage)
				{
					// Arrange
					var packageId = "TestPackage";
					var packageRegistration = CreatePackageRegistration(packageId);
					var user = CreateAdminUser();

					SetupValidPackageAndUser(packageRegistration, user);
					SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, invalidUrl, user))
						.ThrowsAsync(new SponsorshipUrlValidationException(expectedErrorMessage));

					// Act
					var result = await Controller.AddUrl(packageId, invalidUrl);

					// Assert
					AssertRedirectWithError(result, packageId, $"{expectedErrorMessage}");
				}

				[Fact]
				public async Task RedirectsWithErrorWhenGeneralExceptionThrown()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
					var packageRegistration = CreatePackageRegistration(packageId);
					var user = CreateAdminUser();
					var exceptionMessage = "Database error";

					SetupValidPackageAndUser(packageRegistration, user);
					SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
						.ThrowsAsync(new Exception(exceptionMessage));

					// Act
					var result = await Controller.AddUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, $"Error adding sponsorship URL: {exceptionMessage}");
				}
			}

			public class SuccessScenarios : FactsBase
			{
				[Theory]
				[InlineData(TestUrls.ValidGitHubUrl)]
				[InlineData(TestUrls.ValidPatreonUrl)]
				[InlineData(TestUrls.ValidKoFiUrl)]
				public async Task RedirectsWithSuccessWhenUrlAddedSuccessfully(string url)
				{
					// Arrange
					var packageId = "TestPackage";
					var packageRegistration = CreatePackageRegistration(packageId);
					var user = CreateAdminUser();

					SetupSuccessfulAddUrl(packageRegistration, user, url);

					// Act
					var result = await Controller.AddUrl(packageId, url);

					// Assert
					AssertRedirectWithSuccess(result, packageId, "added successfully");
					SponsorshipUrlService.Verify(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user), Times.Once);
				}
			}
		}

		public class TheRemoveUrlMethod : FactsBase
		{
			public class ErrorScenarios : FactsBase
			{
				[Fact]
				public async Task RedirectsWithErrorWhenPackageNotFound()
				{
					// Arrange
					var packageId = "NonExistentPackage";
					var url = TestUrls.ValidGitHubUrl;
					SetupPackageNotFound(packageId);

					// Act
					var result = await Controller.RemoveUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, "not found");
				}

				[Fact]
				public async Task RedirectsWithErrorWhenUserNotAuthenticated()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
					var packageRegistration = CreatePackageRegistration(packageId);

					PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
						.Returns(packageRegistration);
					SponsorshipUrlService.Setup(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, null))
						.ThrowsAsync(new UnauthorizedAccessException("User cannot be null"));
					Controller.SetCurrentUser(null);

					// Act
					var result = await Controller.RemoveUrl(packageId, url);

					// Assert
					AssertRedirectWithError(result, packageId, "Error removing sponsorship URL: User cannot be null");
				}

				[Fact]
				public async Task RedirectsWithErrorWhenExceptionThrown()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
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
					AssertRedirectWithError(result, packageId, $"Error removing sponsorship URL: {exceptionMessage}");
				}
			}

			public class SuccessScenarios : FactsBase
			{
				[Fact]
				public async Task RedirectsWithSuccessWhenUrlRemovedSuccessfully()
				{
					// Arrange
					var packageId = "TestPackage";
					var url = TestUrls.ValidGitHubUrl;
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
					AssertRedirectWithSuccess(result, packageId, "removed successfully");
					SponsorshipUrlService.Verify(x => x.RemoveSponsorshipUrlAsync(packageRegistration, url, user), Times.Once);
				}
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

			protected static class TestUrls
			{
				public const string ValidGitHubUrl = "https://github.com/sponsors/user";
				public const string ValidPatreonUrl = "https://patreon.com/user";
				public const string ValidKoFiUrl = "https://ko-fi.com/user";
			}

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

			protected void SetupPackageNotFound(string packageId)
			{
				PackageService.Setup(x => x.FindPackageRegistrationById(packageId))
					.Returns((PackageRegistration)null);
			}

			protected void SetupValidPackageAndUser(PackageRegistration packageRegistration, User user)
			{
				PackageService.Setup(x => x.FindPackageRegistrationById(packageRegistration.Id))
					.Returns(packageRegistration);
				SponsorshipUrlService.Setup(x => x.GetSponsorshipUrlEntries(packageRegistration))
					.Returns(new List<SponsorshipUrlEntry>());
				TrustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks).Returns(10);
				Controller.SetCurrentUser(user);
			}

			protected void SetupSuccessfulAddUrl(PackageRegistration packageRegistration, User user, string url)
			{
				SetupValidPackageAndUser(packageRegistration, user);
				SponsorshipUrlService.Setup(x => x.AddSponsorshipUrlAsync(packageRegistration, url, user))
					.ReturnsAsync(url);
			}

			protected static void AssertIndexViewModel(ActionResult result, string packageId, bool hasPackage, bool hasMessage, bool isSuccess)
			{
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				
				if (hasPackage)
					Assert.NotNull(model.Package);
				else
					Assert.Null(model.Package);

				if (hasMessage)
					Assert.NotNull(model.Message);
				else
					Assert.Null(model.Message);

				Assert.Equal(isSuccess, model.IsSuccess);
			}

			protected static void AssertIndexViewModelWithError(ActionResult result, string packageId, string expectedMessagePart)
			{
				var viewResult = Assert.IsType<ViewResult>(result);
				var model = Assert.IsType<PackageSponsorshipIndexViewModel>(viewResult.Model);
				Assert.Equal(packageId, model.PackageId);
				Assert.Null(model.Package);
				Assert.Contains(expectedMessagePart, model.Message);
				Assert.False(model.IsSuccess);
			}

			protected static void AssertRedirectWithError(ActionResult result, string packageId, string expectedMessagePart)
			{
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Equal(packageId, redirectResult.RouteValues["packageId"]);
				Assert.Contains(expectedMessagePart, redirectResult.RouteValues["message"].ToString());
				Assert.Equal(false, redirectResult.RouteValues["isSuccess"]);
			}

			protected static void AssertRedirectWithSuccess(ActionResult result, string packageId, string expectedMessagePart)
			{
				var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirectResult.RouteValues["action"]);
				Assert.Equal(packageId, redirectResult.RouteValues["packageId"]);
				Assert.Contains(expectedMessagePart, redirectResult.RouteValues["message"].ToString());
				Assert.Equal(true, redirectResult.RouteValues["isSuccess"]);
			}
		}
	}
}