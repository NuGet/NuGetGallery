// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
	public class ValidationControllerFacts
	{
		public class TheForceFailValidationMethod : FactsBase
		{
			[Fact]
			public async Task ReturnsErrorWhenPackageIdIsEmpty()
			{
				var result = await _target.ForceFailValidation(packageId: "", packageVersion: null);

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Equal("Package ID is required.", _target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenPackageIdIsWhitespace()
			{
				var result = await _target.ForceFailValidation(packageId: "  ", packageVersion: null);

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Equal("Package ID is required.", _target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenPackageNotFound()
			{
				_packageService
					.Setup(x => x.FindPackageByIdAndVersion("NonExistent", null))
					.Returns((Package)null);

				var result = await _target.ForceFailValidation("NonExistent", packageVersion: null);

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("not found", (string)_target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenPackageVersionNotFound()
			{
				_packageService
					.Setup(x => x.FindPackageByIdAndVersionStrict("TestPackage", "1.0.0"))
					.Returns((Package)null);

				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("not found", (string)_target.TempData["ErrorMessage"]);
				Assert.Contains("1.0.0", (string)_target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsInfoWhenPackageAlreadyFailedValidation()
			{
				SetupPackage(PackageStatus.FailedValidation);

				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("already in FailedValidation", (string)_target.TempData["Message"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenPackageIsAvailable()
			{
				SetupPackage(PackageStatus.Available);

				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("Available", (string)_target.TempData["ErrorMessage"]);
				Assert.Contains("cannot be transitioned", (string)_target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenPackageIsDeleted()
			{
				SetupPackage(PackageStatus.Deleted);

				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("Deleted", (string)_target.TempData["ErrorMessage"]);
				Assert.Contains("cannot be modified", (string)_target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task ReturnsErrorWhenInitiatorIsNotAsynchronous()
			{
				SetupPackage(PackageStatus.Validating);

				// Default _packageValidationInitiator mock is not AsynchronousPackageValidationInitiator
				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("not configured for asynchronous validation", (string)_target.TempData["ErrorMessage"]);
			}

			[Fact]
			public async Task FindsLatestVersionWhenVersionNotSpecified()
			{
				SetupPackage(PackageStatus.Validating);

				await _target.ForceFailValidation("TestPackage", packageVersion: null);

				_packageService.Verify(
					x => x.FindPackageByIdAndVersion("TestPackage", null),
					Times.Once);
			}

			[Fact]
			public async Task FindsSpecificVersionWhenVersionSpecified()
			{
				SetupPackage(PackageStatus.Validating);

				await _target.ForceFailValidation("TestPackage", "1.0.0");

				_packageService.Verify(
					x => x.FindPackageByIdAndVersionStrict("TestPackage", "1.0.0"),
					Times.Once);
			}

			[Fact]
			public async Task ReturnsErrorOnException()
			{
				_packageService
					.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
					.Throws(new InvalidOperationException("Something went wrong"));

				var result = await _target.ForceFailValidation("TestPackage", "1.0.0");

				var redirect = Assert.IsType<RedirectToRouteResult>(result);
				Assert.Equal("Index", redirect.RouteValues["action"]);
				Assert.Contains("Something went wrong", (string)_target.TempData["ErrorMessage"]);
			}

			private void SetupPackage(PackageStatus status)
			{
				var package = new Package
				{
					Key = 1,
					PackageRegistration = new PackageRegistration { Id = "TestPackage" },
					NormalizedVersion = "1.0.0",
					Version = "1.0.0",
					PackageStatusKey = status,
				};

				_packageService
					.Setup(x => x.FindPackageByIdAndVersion("TestPackage", null))
					.Returns(package);
				_packageService
					.Setup(x => x.FindPackageByIdAndVersionStrict("TestPackage", "1.0.0"))
					.Returns(package);
			}
		}

		public abstract class FactsBase : TestContainer
		{
			protected readonly Mock<IPackageService> _packageService;
			protected readonly Mock<IPackageValidationInitiator<Package>> _packageValidationInitiator;
			protected readonly ValidationAdminService _validationAdminService;
			protected readonly ValidationController _target;

			public FactsBase()
			{
				_packageService = new Mock<IPackageService>();
				_packageValidationInitiator = new Mock<IPackageValidationInitiator<Package>>();

				_validationAdminService = new ValidationAdminService(
					Mock.Of<IEntityRepository<PackageValidationSet>>(),
					Mock.Of<IEntityRepository<PackageValidation>>(),
					Mock.Of<IEntityRepository<Package>>(),
					Mock.Of<IEntityRepository<SymbolPackage>>(),
					Mock.Of<IValidationService>());

				_target = new ValidationController(
					_validationAdminService,
					_packageService.Object,
					_packageValidationInitiator.Object);
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					_target?.Dispose();
					base.Dispose(disposing);
				}
			}
		}
	}
}
