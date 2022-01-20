// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class LockPackageControllerFacts
    {
        [Fact]
        public async Task UpdateAppliesChangesToTheCorrectPackages()
        {
            // Arrange
            var packageRegistrationsInDb = Enumerable.Range(1, 10).Select(i =>
                new PackageRegistration()
                {
                    Id = "Test" + i,
                    IsLocked = false,
                    Key = i
                }).ToList();

            var packageRegistrationsRepository = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrationsRepository.Setup(x => x.GetAll()).Returns(packageRegistrationsInDb.AsQueryable());

            var controller = new LockPackageController(packageRegistrationsRepository.Object);

            var viewModel = new LockPackageViewModel()
            {
                LockStates = new List<LockState>()
                {
                    new LockState() { Identifier = "Test1", IsLocked = true },
                    new LockState() { Identifier = "Test5", IsLocked = true }
                }
            };

            // Act
            var result = await controller.Update(viewModel);

            // Assert
            Assert.Equal(2, packageRegistrationsInDb.Count(x => x.IsLocked));

            Assert.True(packageRegistrationsInDb.First(x => x.Id == "Test1").IsLocked);
            Assert.True(packageRegistrationsInDb.First(x => x.Id == "Test5").IsLocked);

            var viewResult = ResultAssert.IsView<LockPackageViewModel>(result, "LockIndex");

            Assert.Equal(2, viewResult.LockStates.Count(x => x.IsLocked));
            Assert.True(viewResult.LockStates.First(x => x.Identifier == "Test1").IsLocked);
            Assert.True(viewResult.LockStates.First(x => x.Identifier == "Test5").IsLocked);
        }
    }
}
