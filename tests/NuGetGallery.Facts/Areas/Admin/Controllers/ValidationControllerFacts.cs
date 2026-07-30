// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ValidationControllerFacts
    {
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
                    _validationService.Object,
                    Mock.Of<IAuditingService>());

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
