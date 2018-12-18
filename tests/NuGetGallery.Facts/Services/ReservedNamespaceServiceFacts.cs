// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.TestUtils;
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
            [InlineData(true)]
            [InlineData(false)]
            public async Task RestrictiveNamespaceUnderSharedNamespaceIsMarkedShared(bool isSharedNamespace)
            {
                var namespaces = new List<ReservedNamespace> {
                    new ReservedNamespace("xunit.", isSharedNamespace: false, isPrefix: true),
                    new ReservedNamespace("xunit.extentions.", isSharedNamespace: true, isPrefix: true),
                };

                var newNamespace = new ReservedNamespace("xunit.extentions.someuser.", isSharedNamespace, isPrefix: true);

                var service = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
                await service.AddReservedNamespaceAsync(newNamespace);

                // Commit should happen with shared namespace set to 'true'
                service.MockReservedNamespaceRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == true)));

                service.MockReservedNamespaceRepository.Verify(x => x.CommitChangesAsync());
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task RestrictiveNamespaceUnderPrivateNamespacesIsMarkedAsAppropriate(bool isSharedNamespace)
            {
                var namespaces = new List<ReservedNamespace> {
                    new ReservedNamespace("xunit.", isSharedNamespace: false, isPrefix: true),
                    new ReservedNamespace("xunit.extentions.", isSharedNamespace: false, isPrefix: true),
                };

                var newNamespace = new ReservedNamespace("xunit.extentions.someuser.", isSharedNamespace, isPrefix: true);

                var service = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
                await service.AddReservedNamespaceAsync(newNamespace);

                service.MockReservedNamespaceRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == isSharedNamespace)));

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
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(addNamespace));
            }

            [Theory]
            [InlineData("LooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongNaaaaaaaaaaaaaaaaaaaaaaaaaaaamespace")]
            [InlineData("@InvalidNamespace")]
            [InlineData("Invalid.Ch@rac#ters$-In(Name)space")]
            public async Task InvalidNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();
                var addNamespace = new ReservedNamespace(value, isSharedNamespace: false, isPrefix: true);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(addNamespace));
            }

            [Fact]
            public async Task ExistingNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var newNamespace = testNamespaces.FirstOrDefault();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ExistingNamespaceWithDifferentPrefixStateThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var newNamespace = testNamespaces.FirstOrDefault(x => x.Value == "jquery");
                newNamespace.IsPrefix = !newNamespace.IsPrefix;
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task LiberalNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                // test case has a namespace with "Microsoft." as the value.
                var newNamespace = new ReservedNamespace("Micro", isSharedNamespace: false, isPrefix: true);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task LiberalNamespaceForExactMatchIsAllowed()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
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

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var newNamespace = new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true);

                var service = new TestableReservedNamespaceService();
                await service.AddReservedNamespaceAsync(newNamespace);

                Assert.True(service.AuditingService.WroteRecord<ReservedNamespaceAuditRecord>(ar =>
                    ar.Action == AuditedReservedNamespaceAction.ReserveNamespace
                    && ar.Value == newNamespace.Value));
            }
        }

        public class TheDeleteReservedNamespaceAsyncMethod
        {
            [Fact]
            public async Task VanillaReservedNamespaceIsDeletedCorrectly()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
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
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await service.DeleteReservedNamespaceAsync(existingNamespace.Value);

                Assert.True(service.AuditingService.WroteRecord<ReservedNamespaceAuditRecord>(ar =>
                    ar.Action == AuditedReservedNamespaceAction.UnreserveNamespace
                    && ar.Value == existingNamespace.Value));
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
            public async Task NonexistentNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteReservedNamespaceAsync("Nonexistent.Namespace."));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task DeletingNamespaceClearsVerifiedFlagOnPackage(bool isSharedNamespace)
            {
                var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var registrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var msPrefix = namespaces.First();
                msPrefix.IsSharedNamespace = isSharedNamespace;
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
                            false,
                            true),
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
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
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
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, username));
            }

            [Fact]
            public async Task AddingNonExistentUserToNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, "NonExistentUser"));
            }

            [Fact]
            public async Task AddingExistingOwnerToTheNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner = testUsers.First();
                existingNamespace.Owners.Add(owner);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, owner.Username));
            }

            [Fact]
            public async Task AddAnOwnerToNamespaceSuccessfully()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner = testUsers.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await service.AddOwnerToReservedNamespaceAsync(prefix, owner.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), It.IsAny<bool>()),
                        Times.Never);

                Assert.True(existingNamespace.Owners.Contains(owner));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner = testUsers.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await service.AddOwnerToReservedNamespaceAsync(prefix, owner.Username);

                Assert.True(service.AuditingService.WroteRecord<ReservedNamespaceAuditRecord>(ar =>
                    ar.Action == AuditedReservedNamespaceAction.AddOwner
                    && ar.Value == existingNamespace.Value));
            }

            [Fact]
            public async Task AddingOwnerToNamespaceMarksRegistrationsVerified()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = ReservedNamespaceServiceTestData.GetRegistrations();
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
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), true),
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

            [Fact]
            public async Task AddingOwnerToAbsoluteNamespaceMarksOnlyAbsoluteRegistrationsVerified()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = new ReservedNamespace("Microsoft", isSharedNamespace: false, isPrefix: false);
                testNamespaces.Add(existingNamespace);
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var registrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var pr1 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package1"));
                var pr2 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.Package2"));
                var pr3 = registrations.ToList().FirstOrDefault(pr => (pr.Id == "Microsoft.AspNet.Package2"));
                var pr4 = new PackageRegistration { Id = "Microsoft", IsVerified = false };
                registrations.Add(pr4);

                pr1.Owners.Add(owner1);
                pr2.Owners.Add(owner1);
                pr3.Owners.Add(owner1);
                pr4.Owners.Add(owner1);

                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers, packageRegistrations: registrations);

                Assert.True(existingNamespace.PackageRegistrations.Count() == 0);

                await service.AddOwnerToReservedNamespaceAsync(existingNamespace.Value, owner1.Username);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync());

                service
                    .MockPackageService
                    .Verify(p => p.UpdatePackageVerifiedStatusAsync(
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), true),
                        Times.Once);

                Assert.True(existingNamespace.Owners.Contains(owner1));
                // Only Microsoft.Package1 should match the namespace
                Assert.True(existingNamespace.PackageRegistrations.Count() == 1);
                existingNamespace
                    .PackageRegistrations
                    .ToList()
                    .ForEach(pr =>
                    {
                        Assert.True(pr.IsVerified);
                        Assert.True(pr.Id == pr4.Id);
                    });
                Assert.False(pr1.IsVerified);
                Assert.False(pr3.IsVerified);
                Assert.False(pr2.IsVerified);
            }
        }


        public class TheAddPackageRegistrationToNamespaceMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public void NullNamespaceThrowsException(string value)
            {
                var service = new TestableReservedNamespaceService();

                Assert.Throws<ArgumentException>(() => service.AddPackageRegistrationToNamespace(value, new PackageRegistration()));
            }

            [Fact]
            public void NullPackageRegistrationThrowsException()
            {
                var service = new TestableReservedNamespaceService();

                Assert.Throws<ArgumentNullException>(() => service.AddPackageRegistrationToNamespace("Microsoft.", null));
            }

            [Fact]
            public void NonExistentNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var existingReg = testPackageRegistrations.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, packageRegistrations: testPackageRegistrations);

                Assert.Throws<InvalidOperationException>(() => service.AddPackageRegistrationToNamespace("Non.Existent.Namespace.", existingReg));
            }

            [Fact]
            public void PackageRegistrationIsAddedSuccessfully()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var existingReg = testPackageRegistrations.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, packageRegistrations: testPackageRegistrations);

                service.AddPackageRegistrationToNamespace(existingNamespace.Value, existingReg);

                Assert.True(existingNamespace.PackageRegistrations.Contains(existingReg));
            }

            [Fact]
            public void CommitChangesIsNotExecuted()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var existingReg = testPackageRegistrations.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, packageRegistrations: testPackageRegistrations);

                service.AddPackageRegistrationToNamespace(existingNamespace.Value, existingReg);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync(), Times.Never);
            }
        }

        public class TheRemovePackageRegistrationFromNamespaceMethod
        {
            [Fact]
            public void NullNamespaceThrowsException()
            {
                var service = new TestableReservedNamespaceService();

                Assert.Throws<ArgumentNullException>(() => service.RemovePackageRegistrationFromNamespace(null, new PackageRegistration()));
            }

            [Fact]
            public void NullPackageRegistrationThrowsException()
            {
                var service = new TestableReservedNamespaceService();

                Assert.Throws<ArgumentNullException>(() => service.RemovePackageRegistrationFromNamespace(new ReservedNamespace(), null));
            }

            [Fact]
            public void PackageRegistrationIsRemovedFromNamespaceSuccessfully()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var existingReg = testPackageRegistrations.First();
                existingNamespace.PackageRegistrations.Add(existingReg);
                existingReg.ReservedNamespaces.Add(existingNamespace);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, packageRegistrations: testPackageRegistrations);

                service.RemovePackageRegistrationFromNamespace(existingNamespace, existingReg);

                Assert.False(existingNamespace.PackageRegistrations.Contains(existingReg));
                Assert.False(existingReg.ReservedNamespaces.Contains(existingNamespace));
            }

            [Fact]
            public void CommitChangesIsNotExecuted()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                var existingReg = testPackageRegistrations.First();
                existingNamespace.PackageRegistrations.Add(existingReg);
                existingReg.ReservedNamespaces.Add(existingNamespace);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, packageRegistrations: testPackageRegistrations);

                service.RemovePackageRegistrationFromNamespace(existingNamespace, existingReg);

                service
                    .MockReservedNamespaceRepository
                    .Verify(x => x.CommitChangesAsync(), Times.Never);
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
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
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
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces);

                await Assert.ThrowsAsync<ArgumentException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(existingNamespace.Value, username));
            }

            [Fact]
            public async Task DeletingNonExistentUserFromNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var existingNamespace = testNamespaces.First();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(existingNamespace.Value, "NonExistentUser"));
            }

            [Fact]
            public async Task DeletingNonOwnerFromNamespaceThrowsException()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var user1 = testUsers.First(u => u.Username == "test1");
                var user2 = testUsers.First(u => u.Username == "test2");
                existingNamespace.Owners.Add(user1);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteOwnerFromReservedNamespaceAsync(prefix, user2.Username));
            }

            [Fact]
            public async Task DeleteOwnerFromNamespaceSuccessfully()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
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
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), true),
                        Times.Never);

                Assert.False(existingNamespace.Owners.Contains(owner));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner = testUsers.First();
                existingNamespace.Owners.Add(owner);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                await service.DeleteOwnerFromReservedNamespaceAsync(prefix, owner.Username);

                Assert.True(service.AuditingService.WroteRecord<ReservedNamespaceAuditRecord>(ar =>
                    ar.Action == AuditedReservedNamespaceAction.RemoveOwner
                    && ar.Value == existingNamespace.Value));
            }

            [Fact]
            public async Task DeletingOwnerFromNamespaceMarksRegistrationsUnverified()
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = ReservedNamespaceServiceTestData.GetRegistrations();
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
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), true),
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
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var owner1 = testUsers.First(u => u.Username == "test1");
                var owner2 = testUsers.First(u => u.Username == "test2");
                var registrations = ReservedNamespaceServiceTestData.GetRegistrations();
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
                        It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), true),
                        Times.Once);

                Assert.False(existingNamespace.Owners.Contains(owner1));
                Assert.True(existingNamespace.PackageRegistrations.Count == 1);
                Assert.False(pr1.IsVerified);
                Assert.True(pr2.IsVerified);
            }
        }

        public class TheShouldMarkNewPackageVerifiedMethod
        {
            [Theory]
            [InlineData("Microsoft.Aspnet")]
            [InlineData("microsoft.aspnet")]
            [InlineData("Microsoft.Aspnet.Newpackage")]
            [InlineData("microsoft.aspnet.newpackage")]
            [InlineData("jquery")]
            [InlineData("jQuery")]
            public void NonOwnedNamespacesRejectPush(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(lastUser, lastUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(lastUser, id, out var matchingNamespaces);
                Assert.Empty(matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.ReservedNamespaceFailure, isPushAllowed);
            }

            [Theory]
            [InlineData("Microsoft.Aspnet")]
            [InlineData("microsoft.aspnet")]
            [InlineData("Microsoft.Aspnet.Newpackage")]
            public void SharedNamespacesAllowsPush(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                existingNamespace.IsSharedNamespace = true;
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(lastUser, lastUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(lastUser, id, out var matchingNamespaces);
                Assert.Empty(matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
            }

            [Theory]
            [InlineData("Microsoft.Aspnet")]
            [InlineData("microsoft.aspnet")]
            [InlineData("Microsoft.Aspnet.Newpackage")]
            public void OwnedSharedNamespacesAllowsPushAndReturnsDataCorrectly(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                existingNamespace.IsSharedNamespace = true;
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(firstUser, firstUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(firstUser, id, out var matchingNamespaces);
                Assert.NotEmpty(matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
            }

            [Theory]
            [InlineData("Non.Matching.Id")]
            [InlineData("RandomId")]
            public void NonMatchingNamespacesAllowsPush(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(lastUser, lastUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(lastUser, id, out var matchingNamespaces);
                Assert.Empty(matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
            }

            [Theory]
            [InlineData("jquer")]
            [InlineData("j.query")]
            [InlineData("jqueryextention")]
            [InlineData("jquery.extention")]
            public void NonPrefixNamespaceDoesNotBlockPush(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "jQuery";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                existingNamespace.IsPrefix = false;
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(lastUser, lastUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(lastUser, id, out var matchingNamespaces);
                Assert.Empty(matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
            }

            [Theory]
            [InlineData("Microsoft.Aspnet")]
            [InlineData("microsoft.aspnet")]
            [InlineData("Microsoft.Aspnet.Newpackage")]
            public void OwnedNamespacesAllowsPush(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefix = "microsoft.";
                var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                existingNamespace.Owners.Add(firstUser);
                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(firstUser, firstUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(firstUser, id, out var matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
                Assert.NotEmpty(matchingNamespaces);
                Assert.True(matchingNamespaces.Count() == 1);
            }

            [Theory]
            [InlineData("Microsoft.Aspnet.Newpackage")]
            [InlineData("microsoft.aspnet.newpackage")]
            public void MultipleOwnedNamespacesAreReturnedCorrectly(string id)
            {
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefixes = new List<string> { "microsoft.", "microsoft.aspnet." };
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                prefixes.ForEach(p =>
                {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.Owners.Add(firstUser);
                });

                var service = new TestableReservedNamespaceService(reservedNamespaces: testNamespaces, users: testUsers);

                var isPushAllowed = ActionsRequiringPermissions.UploadNewPackageId.CheckPermissions(firstUser, firstUser, new ActionOnNewPackageContext(id, service));
                var shouldMarkNewPackageVerified = service.ShouldMarkNewPackageIdVerified(firstUser, id, out var matchingNamespaces);
                Assert.Equal(PermissionsCheckResult.Allowed, isPushAllowed);
                Assert.NotEmpty(matchingNamespaces);
                Assert.True(matchingNamespaces.Count() == prefixes.Count());
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
            [InlineData("Multiple-Sequential--hyphens")]
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
            [InlineData("123-Namespace-endswith-hyphen-")]
            [InlineData("123_Namespace_endswith_Underscores_")]
            [InlineData("Multiple_Sequential__Underscores")]
            public void ValidNamespacesDontThrowException(string value)
            {
                var ex = Record.Exception(() => ReservedNamespaceService.ValidateNamespace(value));
                Assert.Null(ex);
            }
        }
    }
}
