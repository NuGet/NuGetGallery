// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
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

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void HasEnabledMultiFactorAuthentication_ForUsers(bool userMfaStatus)
            {
                var controller = GetController<UsersController>();
                var user = new User("theUser")
                {
                    EnableMultiFactorAuthentication = userMfaStatus,
                };
                var currentUser = new User("theCurrentUser");
                var packages = new List<ListPackageItemViewModel>
                {
                    CreatePackageItemViewModel("1.0.0"),
                    CreatePackageItemViewModel("2.0.0")
                };

                // Act
                var profile = new UserProfileModel(user, currentUser, packages, 0, 10, controller.Url);

                // Assert
                Assert.Equal(userMfaStatus, profile.HasEnabledMultiFactorAuthentication);
            }

            [Theory]
            [InlineData(true, false, true, false)]
            [InlineData(true, true, false, false)]
            [InlineData(false, false, true, false)]
            [InlineData(true, true, true, true)]
            public void HasEnabledMultiFactorAuthentication_ForOrganizations(bool user1MfaStatus, bool user2MfaStatus, bool collabUserMfaStatus, bool expectedOrgMfaStatus)
            {
                var controller = GetController<UsersController>();
                var userList = new List<User>() {
                    new User("theUser")
                    {
                        EnableMultiFactorAuthentication = user1MfaStatus
                    },
                    new User("theOtherUser")
                    {
                        EnableMultiFactorAuthentication = user2MfaStatus
                    }
                };

                var collabUser = new User("TheCollabUser")
                {
                    EnableMultiFactorAuthentication = collabUserMfaStatus
                };

                var org = CreateTestOrganization(userList, collabUser);
                var currentUser = new User("theCurrentUser");
                var packages = new List<ListPackageItemViewModel>
                {
                    CreatePackageItemViewModel("1.0.0"),
                    CreatePackageItemViewModel("2.0.0")
                };

                // Act
                var profile = new UserProfileModel(org, currentUser, packages, 0, 10, controller.Url);

                // Assert
                Assert.Equal(expectedOrgMfaStatus, profile.HasEnabledMultiFactorAuthentication);
            }

            private Organization CreateTestOrganization(List<User> usersList, User collabUser)
            {
                var organization = new Organization()
                {
                    Key = 1,
                    Username = "a"
                };

                foreach (var user in usersList)
                {
                    organization.Members.Add(new Membership()
                    {
                        MemberKey = user.Key,
                        Member = user,
                        OrganizationKey = organization.Key,
                        Organization = organization,
                        IsAdmin = true
                    });
                }

                if (collabUser != null)
                {
                    organization.Members.Add(new Membership()
                    {
                        MemberKey = collabUser.Key,
                        Member = collabUser,
                        OrganizationKey = organization.Key,
                        Organization = organization,
                        IsAdmin = false
                    });
                }

                return organization;
            }

            private ListPackageItemViewModel CreatePackageItemViewModel(string version)
            {
                return new ListPackageItemViewModelFactory(Mock.Of<IIconUrlProvider>()).Create(new Package
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