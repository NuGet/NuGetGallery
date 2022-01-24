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
    public class LockUserControllerFacts
    {
        [Fact]
        public async Task UpdateAppliesLockChangesToTheCorrectUsers()
        {
            // Arrange
            var usersInDb = Enumerable.Range(1, 10).Select(i =>
                new User
                {
                    Username = "Username" + i,
                    IsLocked = false,
                    Key = i
                }).ToList();

            var usersRepository = new Mock<IEntityRepository<User>>();
            usersRepository.Setup(x => x.GetAll()).Returns(usersInDb.AsQueryable());

            var controller = new LockUserController(usersRepository.Object);

            var viewModel = new LockUserViewModel
            {
                LockStates = new List<LockState>
                {
                    new LockState() { Identifier = "Username1", IsLocked = true },
                    new LockState() { Identifier = "Username5", IsLocked = true }
                }
            };

            // Act
            var result = await controller.Update(viewModel);

            // Assert
            Assert.Equal(2, usersInDb.Count(x => x.IsLocked));
            Assert.Equal(8, usersInDb.Count(x => !x.IsLocked));

            Assert.True(usersInDb.First(x => x.Username == "Username1").IsLocked);
            Assert.True(usersInDb.First(x => x.Username == "Username5").IsLocked);

            var viewResult = ResultAssert.IsView<LockUserViewModel>(result, "LockIndex");

            Assert.Equal(2, viewResult.LockStates.Count(x => x.IsLocked));
            Assert.True(viewResult.LockStates.First(x => x.Identifier == "Username1").IsLocked);
            Assert.True(viewResult.LockStates.First(x => x.Identifier == "Username5").IsLocked);
        }

        [Fact]
        public async Task UpdateAppliesUnlockChangesToTheCorrectUsers()
        {
            // Arrange
            var usersInDb = Enumerable.Range(1, 10).Select(i =>
                new User
                {
                    Username = "Username" + i,
                    IsLocked = true,
                    Key = i
                }).ToList();

            var usersRepository = new Mock<IEntityRepository<User>>();
            usersRepository.Setup(x => x.GetAll()).Returns(usersInDb.AsQueryable());

            var controller = new LockUserController(usersRepository.Object);

            var viewModel = new LockUserViewModel
            {
                LockStates = new List<LockState>
                {
                    new LockState() { Identifier = "Username1", IsLocked = false },
                    new LockState() { Identifier = "Username5", IsLocked = false }
                }
            };

            // Act
            var result = await controller.Update(viewModel);

            // Assert
            Assert.Equal(2, usersInDb.Count(x => !x.IsLocked));
            Assert.Equal(8, usersInDb.Count(x => x.IsLocked));

            Assert.False(usersInDb.First(x => x.Username == "Username1").IsLocked);
            Assert.False(usersInDb.First(x => x.Username == "Username5").IsLocked);

            var viewResult = ResultAssert.IsView<LockUserViewModel>(result, "LockIndex");

            Assert.Equal(2, viewResult.LockStates.Count(x => !x.IsLocked));
            Assert.False(viewResult.LockStates.First(x => x.Identifier == "Username1").IsLocked);
            Assert.False(viewResult.LockStates.First(x => x.Identifier == "Username5").IsLocked);
        }
    }
}
