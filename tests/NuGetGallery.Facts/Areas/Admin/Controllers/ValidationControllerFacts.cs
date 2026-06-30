// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
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
            public async Task ForcesFailureForPendingPackages()
            {
                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[]
                    {
                        new Package { Key = 1, PackageStatusKey = PackageStatus.Available },
                        new Package { Key = 2, PackageStatusKey = PackageStatus.Validating },
                        new Package { Key = 3, PackageStatusKey = PackageStatus.Validating },
                    }.AsQueryable());

                var result = await _target.ForceFailValidation(ValidatingType.Package);

                var redirect = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("Pending", redirect.RouteValues["action"]);
                _validationService.Verify(x => x.FailValidationAsync(It.IsAny<Package>()), Times.Exactly(2));
                Assert.Contains("2", (string)_target.TempData["Message"]);
                Assert.Contains(PackageStatus.FailedValidation.ToString(), (string)_target.TempData["Message"]);
            }

            [Fact]
            public async Task ForcesFailureForPendingSymbolPackages()
            {
                _symbolPackages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[]
                    {
                        new SymbolPackage { Key = 1, StatusKey = PackageStatus.Available },
                        new SymbolPackage { Key = 2, StatusKey = PackageStatus.Validating },
                    }.AsQueryable());

                var result = await _target.ForceFailValidation(ValidatingType.SymbolPackage);

                var redirect = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("Pending", redirect.RouteValues["action"]);
                _validationService.Verify(x => x.FailValidationAsync(It.IsAny<SymbolPackage>()), Times.Once);
            }

            [Fact]
            public async Task ReportsWhenThereAreNoPendingValidations()
            {
                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => Enumerable.Empty<Package>().AsQueryable());

                var result = await _target.ForceFailValidation(ValidatingType.Package);

                var redirect = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("Pending", redirect.RouteValues["action"]);
                _validationService.Verify(x => x.FailValidationAsync(It.IsAny<Package>()), Times.Never);
                Assert.Contains("no", (string)_target.TempData["Message"]);
            }
        }

        public abstract class FactsBase : TestContainer
        {
            protected readonly Mock<IEntityRepository<Package>> _packages;
            protected readonly Mock<IEntityRepository<SymbolPackage>> _symbolPackages;
            protected readonly Mock<IValidationService> _validationService;
            protected readonly ValidationAdminService _validationAdminService;
            protected readonly ValidationController _target;

            public FactsBase()
            {
                _packages = new Mock<IEntityRepository<Package>>();
                _symbolPackages = new Mock<IEntityRepository<SymbolPackage>>();
                _validationService = new Mock<IValidationService>();

                _validationAdminService = new ValidationAdminService(
                    Mock.Of<IEntityRepository<PackageValidationSet>>(),
                    Mock.Of<IEntityRepository<PackageValidation>>(),
                    _packages.Object,
                    _symbolPackages.Object,
                    _validationService.Object);

                _target = new ValidationController(
                    _validationAdminService);
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
