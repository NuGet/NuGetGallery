// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
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
                var packageViewModel = new ListPackageItemViewModel(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        DownloadCount = int.MaxValue
                    },
                    Version = "1.0.0"
                });
                var packages = new List<ListPackageItemViewModel> { packageViewModel, packageViewModel };

                // Act
                var profile = new UserProfileModel(user, packages, 0, 10, controller.Url);

                // Assert
                long expected = (long)int.MaxValue * 2;
                Assert.Equal(expected, profile.TotalPackageDownloadCount);
            }
        }
    }
}