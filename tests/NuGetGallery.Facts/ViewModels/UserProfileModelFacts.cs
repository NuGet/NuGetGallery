// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class UserProfileModelFacts
    {
        public class TheTotalDownloadCountProperty : TestContainer
        {
            [Fact]
            public void TotalDownloadCount_DoesNotThrowIntegerOverflow()
            {
                // Arrange
                var controller = GetController<UsersController>();
                var user = new User("theUser");
                var currentUser = new User("theCurrentUser");
                var packages = new List<ListPackageItemViewModel>
                {
                    CreatePackageItemViewModel("1.0.0"),
                    CreatePackageItemViewModel("2.0.0")
                };

                // Act
                var profile = new UserProfileModel(user, currentUser, packages, 0, 10, controller.Url);

                // Assert
                long expected = (long)int.MaxValue * 2;
                Assert.Equal(expected, profile.TotalPackageDownloadCount);
            }

            private ListPackageItemViewModel CreatePackageItemViewModel(string version)
            {
                return new ListPackageItemViewModelFactory().Create(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        DownloadCount = int.MaxValue
                    },
                    Version = version
                }, currentUser: null);
            }
        }
    }
}