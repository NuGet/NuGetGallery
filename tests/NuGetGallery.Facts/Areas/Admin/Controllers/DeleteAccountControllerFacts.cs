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

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteAccountControllerFacts
    {
        private Fakes _fakes = new Fakes();

        [Fact]
        public void CtorThrowsOnNullArg()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new DeleteAccountController(null));
        }

        public class TheSearchMethod
        {
            private Fakes _fakes = new Fakes();

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
                var searchResult = SearchAccount(_fakes.User.Username, _fakes.User);

                // Assert
                Assert.Single(searchResult);
            }

            [Fact]
            public void WhenOrganization_ReturnsMatch()
            {
                // Arrange & Act
                var searchResult = SearchAccount(_fakes.Organization.Username, _fakes.Organization);

                // Assert
                Assert.Single(searchResult);
            }

            [Fact]
            public void WhenNotFound_ReturnsEmpty()
            {
                // Arrange & Act
                var searchResult = SearchAccount("UserNotFound");

                // Assert
                Assert.Empty(searchResult);
            }

            private List<DeleteAccountSearchResult> SearchAccount(string userName, User findByUsernameResult = null, int findByUsernameTimes = 1)
            {
                // Arrange
                var userService = new Mock<IUserService>();

                userService.Setup(m => m.FindByUsername(It.IsAny<string>()))
                    .Returns(findByUsernameResult)
                    .Verifiable();

                var controller = new DeleteAccountController(userService.Object);

                // Act
                var result = controller.Search(userName) as JsonResult;

                // Assert
                userService.Verify(
                    m => m.FindByUsername(userName), 
                    Times.Exactly(findByUsernameTimes));

                Assert.NotNull(result);
                return result.Data as List<DeleteAccountSearchResult>;
            }
        }
    }
}
