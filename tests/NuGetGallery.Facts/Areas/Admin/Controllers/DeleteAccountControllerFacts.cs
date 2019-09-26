// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using Moq;
using Xunit;
using NuGet.Services.Entities;
using System.Threading.Tasks;
using Moq.Language.Flow;
using Moq.Language;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteAccountControllerFacts : TestContainer
    {
        [Fact]
        public void CtorThrowsOnNullArg()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new DeleteAccountController(null));
        }

        public class TheSearchMethod : TestContainer
        {
            private Fakes _fakes;

            public TheSearchMethod()
            {
                _fakes = Get<Fakes>();
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenQueryMissing_ReturnsEmpty(string query)
            {
                // Arrange & Act
                var searchResult = SearchAccount(query, findByUsernameTimes: 0);

                // Assert
                Assert.Empty(searchResult);
            }

            [Fact]
            public void WhenUser_ReturnsMatch()
            {
                // Arrange & Act
                var user = _fakes.User;
                var searchResult = SearchAccount(user.Username, user);

                // Assert
                var result = Assert.Single(searchResult);
                AssertDeleteAccountSearchResult(user, result);
            }

            [Fact]
            public void WhenOrganization_ReturnsMatch()
            {
                // Arrange & Act
                var organization = _fakes.Organization;
                var searchResult = SearchAccount(organization.Username, organization);

                // Assert
                var result = Assert.Single(searchResult);
                AssertDeleteAccountSearchResult(organization, result);
            }

            [Fact]
            public void WhenNotFound_ReturnsEmpty()
            {
                // Arrange & Act
                _fakes.ShaUser.IsDeleted = true;

                var searchResult = SearchAccount("UserNotFound");

                // Assert
                Assert.Empty(searchResult);
            }

            [Fact]
            public void WhenDeletedUser_ReturnsEmpty()
            {
                // Arrange & Act
                var deletedUser = _fakes.ShaUser;
                deletedUser.IsDeleted = true;

                var searchResult = SearchAccount(deletedUser.Username, deletedUser);

                // Assert
                var result = Assert.Single(searchResult);
                AssertDeleteAccountSearchResult(deletedUser, result);
            }

            private List<DeleteAccountSearchResult> SearchAccount(string userName, User findByUsernameResult = null, int findByUsernameTimes = 1)
            {
                // Arrange
                var userService = GetMock<IUserService>();

                userService
                    .Setup(m => m.FindByUsername(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(findByUsernameResult)
                    .Verifiable();

                var controller = GetController<DeleteAccountController>();

                // Act
                var result = controller.Search(userName) as JsonResult;

                // Assert
                userService.Verify(
                    m => m.FindByUsername(userName, true), 
                    Times.Exactly(findByUsernameTimes));

                Assert.NotNull(result);
                return result.Data as List<DeleteAccountSearchResult>;
            }

            private void AssertDeleteAccountSearchResult(User user, DeleteAccountSearchResult result)
            {
                Assert.Equal(user.Username, result.AccountName);
                Assert.Equal(user.IsDeleted, result.IsDeleted);
                Assert.NotNull(result.ProfileLink);
                Assert.Equal(user.IsDeleted, result.DeleteLink == null);
                Assert.Equal(user.IsDeleted, result.RenameLink != null);
            }
        }

        public class TheRenameMethod : TestContainer
        {
            private const string Username = "user";

            [Fact]
            public async Task WhenUserServiceThrowsUserSafeException_SetsErrorMessage()
            {
                var exceptionMessage = "woops";
                var controller = await InvokeAndAssertRedirectToIndex(
                    setup => setup.ThrowsAsync(new UserSafeException(exceptionMessage)));

                Assert.Equal(exceptionMessage, controller.TempData["ErrorMessage"]);
            }

            [Fact]
            public async Task Success()
            {
                var controller = await InvokeAndAssertRedirectToIndex(
                    setup => setup.Completes());

                Assert.Equal(
                    $"The account named {Username} has been successfully renamed.",
                    controller.TempData["Message"]);
            }

            private async Task<DeleteAccountController> InvokeAndAssertRedirectToIndex(Func<ISetup<IUserService, Task>, IVerifies> setupRenameDeletedAccount)
            {
                var user = new User { Key = 1 };

                var userService = GetMock<IUserService>();
                userService
                    .Setup(x => x.FindByUsername(Username, true))
                    .Returns(user)
                    .Verifiable();

                var renameDeletedAccountSetup = userService.Setup(x => x.RenameDeletedAccount(user));
                setupRenameDeletedAccount(renameDeletedAccountSetup).Verifiable();

                var controller = GetController<DeleteAccountController>();
                var result = await controller.Rename(Username);

                userService.Verify();

                ResultAssert.IsRedirectToRoute(result, new { action = nameof(DeleteAccountController.Index) });

                return controller;
            }
        }
    }
}
