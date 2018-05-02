// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using Moq;
using Xunit;


namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteAccountControllerFacts
    {
        [Fact]
        public void CtorThrowsOnNullArg()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new DeleteAccountController(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void SearchNullOrEmptyQueryAccounts(string query)
        {
            // Arrange
            var userService = new Mock<IUserService>();
            var controller = new DeleteAccountController(userService.Object);

            // Act
            var searchResult = controller.Search(query);

            // Assert
            var data = (List<DeleteAccountSearchResult>)((JsonResult)searchResult).Data;
            Assert.Equal<int>(0, data.Count);
        }

        [Fact]
        public void SearchNotExistentUser()
        {
            // Arrange
            User currentUser = null;
            var userService = new Mock<IUserService>();
            userService.Setup(m => m.FindByUsername(It.IsAny<string>(), false)).Returns(currentUser);
            var controller = new DeleteAccountController(userService.Object);

            // Act
            var searchResult = controller.Search("SomeAccount");

            // Assert
            var data = (List<DeleteAccountSearchResult>)((JsonResult)searchResult).Data;
            Assert.Equal<int>(0, data.Count);
        }

        [Fact]
        public void SearchDeletedAccont()
        {
            // Arrange
            var userName = "TestUser";
            var currentUser = new User()
            {
                Username = userName,
                IsDeleted = true
            };

            var userService = new Mock<IUserService>();
            userService.Setup(m => m.FindByUsername(userName, false)).Returns(currentUser);
            var controller = new DeleteAccountController(userService.Object);

            // Act
            var searchResult = controller.Search(userName);

            // Assert
            var data = (List<DeleteAccountSearchResult>)((JsonResult)searchResult).Data;
            Assert.Equal<int>(0, data.Count);
        }

        [Fact]
        public void SearchHappyAccont()
        {
            // Arrange
            var userName = "TestUser";
            var currentUser = new User()
            {
                Username = userName,
                IsDeleted = false
            };

            var userService = new Mock<IUserService>();
            userService.Setup(m => m.FindByUsername(userName, false)).Returns(currentUser);
            var controller = new DeleteAccountController(userService.Object);

            // Act
            var searchResult = controller.Search(userName);

            // Assert
            var data = (List<DeleteAccountSearchResult>)((JsonResult)searchResult).Data;
            Assert.Equal<int>(1, data.Count);
        }
    }
}
