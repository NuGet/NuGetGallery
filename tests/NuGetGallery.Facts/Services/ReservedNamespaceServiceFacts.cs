// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGetGallery.TestUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Services
{
    public class ReservedNamespaceServiceFacts
    {
        public class TheAddReservedNamespaceAsyncMethod
        {
            [Theory]
            [InlineData("NewNamespace", false, true)]
            [InlineData("NewNamespace.", true, true)]
            [InlineData("New.Namespace", false, false)]
            [InlineData("New.Namespace.Exact", true, false)]
            public async Task NewNamespaceIsReservedCorrectly(string value, bool isShared, bool isPrefix)
            {
                var newNamespace = new ReservedNamespace(value, isSharedNamespace: isShared, isPrefix: isPrefix);

                var service = new TestableReservedNamespaceService();
                await service.AddReservedNamespaceAsync(newNamespace);

                service.MockReservedNamespaceRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                service.MockReservedNamespaceRepository.Verify(x => x.CommitChangesAsync());
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("    ")]
            public async Task EmptyOrNullNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();
                var addNamespace = new ReservedNamespace(value, isSharedNamespace: false, isPrefix: true);
                await Assert.ThrowsAsync<ArgumentException>(async () => await service.AddReservedNamespaceAsync(addNamespace));
            }

            [Theory]
            [InlineData("LooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongNaaaaaaaaaaaaaaaaaaaaaaaaaaaamespace")]
            [InlineData("@InvalidNamespace")]
            [InlineData("Invalid.Ch@rac#ters$-In(Name)space")]
            public async Task InvalidNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();
                var addNamespace = new ReservedNamespace(value, isSharedNamespace: false, isPrefix: true);
                await Assert.ThrowsAsync<ArgumentException>(async () => await service.AddReservedNamespaceAsync(addNamespace));
            }

            [Fact]
            public async Task ExistingNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var newNamespace = testNamespaces.FirstOrDefault();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ExistingNamespaceWithDifferentPrefixStateThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var newNamespace = testNamespaces.FirstOrDefault(x => x.Value == "jquery");
                newNamespace.IsPrefix = !newNamespace.IsPrefix;
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task LiberalNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                // test case has a namespace with "Microsoft." as the value.
                var newNamespace = new ReservedNamespace("Micro", isSharedNamespace: false, isPrefix: true);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task LiberalNamespaceForExactMatchIsAllowed()
            {
                var testNamespaces = GetTestNamespaces();
                // test case has a namespace with "Microsoft." as the value.
                var newNamespace = new ReservedNamespace("Micro", isSharedNamespace: false, isPrefix: false /*exact match*/);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await service.AddReservedNamespaceAsync(newNamespace);

                service.MockReservedNamespaceRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                service.MockReservedNamespaceRepository.Verify(x => x.CommitChangesAsync());
            }
        }

        public class TheDeleteReservedNamespaceAsyncMethod
        {
            [Fact]
            public async Task VanillaReservedNamespaceIsDeletedCorrectly()
            {
                var testNamespaces = GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await service.DeleteReservedNamespaceAsync(existingNamespace.Value);

                service.MockReservedNamespaceRepository.Verify(
                    x => x.DeleteOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == existingNamespace.Value
                                && rn.IsPrefix == existingNamespace.IsPrefix
                                && rn.IsSharedNamespace == existingNamespace.IsSharedNamespace)));

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());
                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public async Task InvalidNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.DeleteReservedNamespaceAsync(value));
            }

            [Fact]
            public async Task NonExistingNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteReservedNamespaceAsync("NonExistent.Namespace."));
            }

            [Fact]
            public async Task DeletingNamespaceClearsVerifiedFlagOnPackage()
            {
                var namespaces = GetTestNamespaces();
                var registrations = GetRegistrations();
                var msPrefix = namespaces.First();
                msPrefix.PackageRegistrations = registrations.Where(x => x.Id.StartsWith(msPrefix.Value)).ToList();
                msPrefix.PackageRegistrations.ToList().ForEach(x => x.ReservedNamespaces.Add(msPrefix));

                var service = new TestableReservedNamespaceService(reservedNamespaces: namespaces, packageRegistrations: registrations);
                await service.DeleteReservedNamespaceAsync(msPrefix.Value);

                var registrationsShouldUpdate = msPrefix.PackageRegistrations;
                service
                    .MockReservedNamespaceRepository
                    .Verify(
                        x => x.DeleteOnCommit(
                            It.Is<ReservedNamespace>(
                                rn => rn.Value == msPrefix.Value
                                    && rn.IsPrefix == msPrefix.IsPrefix
                                    && rn.IsSharedNamespace == msPrefix.IsSharedNamespace)));

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());
                service
                    .MockPackageService
                    .Verify(
                        p => p.UpdatePackageVerifiedStatusAsync(
                            It.Is<IReadOnlyCollection<PackageRegistration>>(
                                list => list.Count() == registrationsShouldUpdate.Count()
                                    && list.Any(pr => registrationsShouldUpdate.Any(ru => ru.Id == pr.Id))),
                            false),
                        Times.Once);

                msPrefix.PackageRegistrations.ToList().ForEach(rn => Assert.False(rn.IsVerified));
            }
        }

        public class TheAddOwnerToReservedNamespaceAsyncMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public async Task InvalidNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.AddOwnerToReservedNamespaceAsync(value, "test1"));
            }

            [Fact]
            public async Task NonExistentNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var testUsers = GetTestUsers();
                var existingUser = testUsers.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddOwnerToReservedNamespaceAsync("NonExistent.Namespace.", existingUser.Username));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public async Task AddingInvalidOwnerToNamespaceThrowsException(string username)
            {
                var testNamespaces = GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, username));
            }

            [Fact]
            public async Task AddingNonExistentUserToNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testUsers = GetTestUsers();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, "NonExistentUser"));
            }

            [Fact]
            public async Task AddAnOwnerToNamespaceSuccessfully()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var owner = testUsers.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await service.AddOwnerToReservedNamespaceAsync(prefix, owner.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()),
                        Times.Never);

                Assert.True(existingNamespace.Owners.Contains(owner));
            }

            [Fact]
            public async Task AddingOwnerToNamespaceMarksRegistrationsVerified()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = GetRegistrations();
                var pr1 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package1"));
                var pr2 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package2"));
                var pr3 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.AspNet.Package2"));
                pr1.Owners.Add(owner1);
                pr2.Owners.Add(owner1);
                pr3.Owners.Add(owner2);

                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers, packageRegistrations: registrations);

                Assert.True(existingNamespace.PackageRegistrations.Count() == 0);

                await service.AddOwnerToReservedNamespaceAsync(prefix, owner1.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()),
                        Times.Once);

                Assert.True(existingNamespace.Owners.Contains(owner1));
                // Only Microsoft.Package1 should match the namespace
                Assert.True(existingNamespace.PackageRegistrations.Count() == 2);
                existingNamespace
                    .PackageRegistrations
                    .ToList()
                    .ForEach(pr =>
                    {
                        Assert.True(pr.IsVerified);
                        Assert.True(pr.Id == pr1.Id || pr.Id == pr2.Id);
                    });
            }
        }

        public class TheDeleteOwnerFromReservedNamespaceAsyncMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public async Task NullNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(value, "test1"));
            }

            [Fact]
            public async Task NonExistentNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var testUsers = GetTestUsers();
                var existingUser = testUsers.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync("Non.Existent.Namespace.", existingUser.Username));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public async Task DeletingInvalidOwnerFromNamespaceThrowsException(string username)
            {
                var testNamespaces = GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(existingNamespace.Value, username));
            }

            [Fact]
            public async Task DeletingNonExistentUserFromNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testUsers = GetTestUsers();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(existingNamespace.Value, "NonExistentUser"));
            }

            [Fact]
            public async Task DeletingNonOwnerFromNamespaceThrowsException()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var user1 = testUsers.First(u => u.Username == "test1");
                var user2 = testUsers.First(u => u.Username == "test2");
                existingNamespace.Owners.Add(user1);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(prefix, user2.Username));
            }

            [Fact]
            public async Task DeleteOwnerFromNamespaceSuccessfully()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var owner = testUsers.First();
                existingNamespace.Owners.Add(owner);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await service.DeleteOwnerFromReservedNamespaceAsync(prefix, owner.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()),
                        Times.Never);

                Assert.False(existingNamespace.Owners.Contains(owner));
            }

            [Fact]
            public async Task DeletingOwnerFromNamespaceMarksRegistrationsUnverified()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = GetRegistrations();
                var pr1 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package1"));
                var pr2 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package2"));
                var pr3 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.AspNet.Package2"));
                pr1.Owners.Add(owner1);
                pr2.Owners.Add(owner1);
                pr3.Owners.Add(owner2);
                pr1.IsVerified = true;
                pr2.IsVerified = true;
                pr3.IsVerified = true;
                existingNamespace.Owners.Add(owner1);
                existingNamespace.PackageRegistrations.Add(pr1);
                existingNamespace.PackageRegistrations.Add(pr2);

                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers, packageRegistrations: registrations);

                Assert.True(existingNamespace.PackageRegistrations.Count == 2);

                await service.DeleteOwnerFromReservedNamespaceAsync(prefix, owner1.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()),
                        Times.Once);

                Assert.False(existingNamespace.Owners.Contains(owner1));
                Assert.True(existingNamespace.PackageRegistrations.Count == 0);
                Assert.False(pr1.IsVerified);
                Assert.False(pr2.IsVerified);
                Assert.True(pr3.IsVerified);
            }

            [Fact]
            public async Task DeletingOwnerFromMultipleOwnedNamespaceDoesNotMarkPackagesUnVerfied()
            {
                var testNamespaces = GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = GetRegistrations();
                var pr1 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package1"));
                var pr2 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package2"));
                pr1.Owners.Add(owner1);
                pr2.Owners.Add(owner1);
                pr2.Owners.Add(owner2);
                pr1.IsVerified = true;
                pr2.IsVerified = true;
                existingNamespace.Owners.Add(owner1);
                existingNamespace.Owners.Add(owner2);
                existingNamespace.PackageRegistrations.Add(pr1);
                existingNamespace.PackageRegistrations.Add(pr2);

                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers, packageRegistrations: registrations);

                Assert.True(existingNamespace.PackageRegistrations.Count == 2);

                await service.DeleteOwnerFromReservedNamespaceAsync(prefix, owner1.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()),
                        Times.Once);

                Assert.False(existingNamespace.Owners.Contains(owner1));
                Assert.True(existingNamespace.PackageRegistrations.Count == 1);
                Assert.False(pr1.IsVerified);
                Assert.True(pr2.IsVerified);
            }
        }

        public class ValidateNamespaceMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            [InlineData("@startsWithSpecialChars")]
            [InlineData("ends.With.Special.Char$")]
            [InlineData("Cont@ins$pecia|C#aracters")]
            [InlineData("Endswithperods..")]
            [InlineData("Multiple.Sequential..Periods.")]
            public void InvalidNamespacesThrowsException(string value)
            {
                Assert.Throws<ArgumentException>(() => ReservedNamespaceService.ValidateNamespace(value));
            }

            [Theory]
            [InlineData("Namespace")]
            [InlineData("Nam-e_s.pace")]
            [InlineData("Name.Space.")]
            [InlineData("123_Name.space.")]
            [InlineData("123-Namespace.")]
            public void ValidNamespacesDontThrowException(string value)
            {
                var ex = Record.Exception(() => ReservedNamespaceService.ValidateNamespace(value));
                Assert.Null(ex);
            }
        }

        private static IList<ReservedNamespace> GetTestNamespaces()
        {
            var result = new List<ReservedNamespace>();
            result.Add(new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("baseTest.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("jquery", isSharedNamespace: false, isPrefix: false));
            result.Add(new ReservedNamespace("jquery.Extentions.", isSharedNamespace: true, isPrefix: true));
            result.Add(new ReservedNamespace("Random.", isSharedNamespace: false, isPrefix: true));

            return result;
        }

        private static IList<PackageRegistration> GetRegistrations()
        {
            var result = new List<PackageRegistration>();
            result.Add(new PackageRegistration { Id = "Microsoft.Package1", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Microsoft.Package2", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Microsoft.AspNet.Package2", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Random.Package1", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery.Extentions.OwnerView", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery.Extentions.ThirdPartyView", IsVerified = false });
            result.Add(new PackageRegistration { Id = "DeltaX.Test1", IsVerified = false });

            return result;
        }

        private static IList<User> GetTestUsers()
        {
            var result = new List<User>();
            result.Add(new User("test1"));
            result.Add(new User("test2"));
            result.Add(new User("test3"));
            result.Add(new User("test4"));
            result.Add(new User("test5"));

            return result;
        }
    }
}
