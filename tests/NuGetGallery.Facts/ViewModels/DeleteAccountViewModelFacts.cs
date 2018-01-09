// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DeleteAccountViewModelFacts
    {
        public class TheHasOrphanPackagesProperty
        {
            [Fact]
            public void WhenPackagesNotSet_ReturnsFalse()
            {
                var viewModel = new DeleteAccountViewModel();

                Assert.False(viewModel.HasOrphanPackages);
            }

            [Fact]
            public void WhenPackageHasMultipleUserOwners_ReturnsFalse()
            {
                var user = new User("theUser") { Key = 1 };
                var user2 = new User("theOtherUser") { Key = 2 };
                var viewModel = CreateViewModel(user, user2);

                Assert.Equal(2, viewModel.Packages.First().Owners.Count);
                Assert.False(viewModel.HasOrphanPackages);
            }

            [Fact]
            public void WhenPackageHasSingleOrgOwnerWithMultipleMembers_ReturnsFalse()
            {
                var organization = new Organization();
                for (int i = 0; i < 2; i++)
                {
                    var user = new User($"theuser{i}") { Key = i };
                    var membership = new Membership()
                    {
                        Organization = organization,
                        Member = user,
                        IsAdmin = false
                    };
                    organization.Members.Add(membership);
                    user.Organizations.Add(membership);
                }
                
                var viewModel = CreateViewModel(organization);

                Assert.Equal(2, organization.Members.Count);
                Assert.Equal(1, viewModel.Packages.First().Owners.Count);
                Assert.False(viewModel.HasOrphanPackages);
            }

            [Fact]
            public void WhenPackageHasSingleUserOwner_ReturnsTrue()
            {
                var user = new User("theUser");
                var viewModel = CreateViewModel(user);
                
                Assert.Equal(1, viewModel.Packages.First().Owners.Count);
                Assert.True(viewModel.HasOrphanPackages);
            }

            [Fact]
            public void WhenPackageHasSingleOrgOwnerWithSingleMember_ReturnsTrue()
            {
                var organization = new Organization();
                var user = new User();
                var membership = new Membership()
                {
                    Organization = organization,
                    Member = user,
                    IsAdmin = false
                };
                organization.Members.Add(membership);
                user.Organizations.Add(membership);
                
                var viewModel = CreateViewModel(organization);

                Assert.Equal(1, organization.Members.Count);
                Assert.Equal(1, viewModel.Packages.First().Owners.Count);
                Assert.True(viewModel.HasOrphanPackages);
            }

            private DeleteAccountViewModel CreateViewModel(params User[] owners)
            {
                var package = new Package()
                {
                    Version = "1.0.0"
                };
                var packageRegistration = new PackageRegistration()
                {
                    Id = "thePackage",
                    Packages = new[] { package },
                    Owners = owners
                };
                package.PackageRegistration = packageRegistration;

                return new DeleteAccountViewModel()
                {
                    Packages = new List<ListPackageItemViewModel>()
                    {
                        new ListPackageItemViewModel(package, currentUser: null)
                    }
                };
            }
        }
    }
}
