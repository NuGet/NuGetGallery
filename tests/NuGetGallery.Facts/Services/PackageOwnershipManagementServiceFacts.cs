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

namespace NuGetGallery
{
    public class PackageOwnershipManagementServiceFacts
    {
        private static PackageOwnershipManagementService CreateService(
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IPackageService> packageService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IPackageOwnerRequestService> packageOwnerRequestService = null,
            IAuditingService auditingService = null,
            bool useDefaultSetup = true)
        {
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            var database = new Mock<IDatabase>();
            database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
            entitiesContext.Setup(m => m.GetDatabase()).Returns(database.Object);
            packageService = packageService ?? new Mock<IPackageService>();
            reservedNamespaceService = reservedNamespaceService ?? new Mock<IReservedNamespaceService>();
            packageOwnerRequestService = packageOwnerRequestService ?? new Mock<IPackageOwnerRequestService>();
            auditingService = auditingService ?? new TestAuditingService();

            if (useDefaultSetup)
            {

                packageService
                    .Setup(x => x.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), true))
                    .Returns(Task.CompletedTask)
                    .Verifiable();
                packageService
                    .Setup(x => x.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), It.IsAny<bool>()))
                    .Returns((IReadOnlyCollection<PackageRegistration> list, bool isVerified, bool commitChanges) =>
                    {
                        list.ToList().ForEach(item => item.IsVerified = isVerified);
                        return Task.CompletedTask;
                    })
                    .Verifiable();
                packageService
                    .Setup(x => x.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<bool>()))
                    .Callback<PackageRegistration, User, bool>((pr, user, commitChanges) => pr.Owners.Remove(user))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                reservedNamespaceService.Setup(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>())).Verifiable();
                reservedNamespaceService
                    .Setup(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<ReservedNamespace>(), It.IsAny<PackageRegistration>()))
                    .Callback<ReservedNamespace, PackageRegistration>((rn, pr) =>
                    {
                        rn.PackageRegistrations.Remove(pr);
                        pr.ReservedNamespaces.Remove(rn);
                    })
                    .Verifiable();

                packageOwnerRequestService
                    .Setup(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()))
                    .Returns<PackageRegistration, User, User>((pr, ro, no) => new[]
                    {
                        new PackageOwnerRequest
                        {
                            PackageRegistration = pr ?? new PackageRegistration { Id = "NuGet.Versioning" },
                            RequestingOwner = ro ?? new User { Username = "NuGet" },
                            NewOwner = no ?? new User { Username = "Microsoft" },
                        },
                    }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                packageOwnerRequestService.Setup(x => x.AddPackageOwnershipRequest(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(Task.FromResult(new PackageOwnerRequest())).Verifiable();
            }

            var packageOwnershipManagementService = new Mock<PackageOwnershipManagementService>(
                entitiesContext.Object,
                packageService.Object,
                reservedNamespaceService.Object,
                packageOwnerRequestService.Object,
                auditingService);

            return packageOwnershipManagementService.Object;
        }

        public class TheAddPackageOwnerAsyncMethod
        {
            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnerAsync(packageRegistration: null, user: testUsers.First()));
            }

            [Fact]
            public async Task NullUserThrowsException()
            {
                var service = CreateService();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnerAsync(packageRegistration: testPackageRegistrations.First(), user: null));
            }

            [Fact]
            public async Task NewOwnerIsAddedSuccessfullyToTheRegistration()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();

                var service = CreateService(packageService: packageService, packageOwnerRequestService: packageOwnerRequestService);
                await service.AddPackageOwnerAsync(package, pendingOwner);

                packageService.Verify(x => x.AddPackageOwnerAsync(package, pendingOwner, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true));
            }

            [Fact]
            public async Task NewOwnerIsAddedSuccessfullyWithoutPendingRequest()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), true)).Returns(Task.CompletedTask).Verifiable();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequests(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new List<PackageOwnerRequest>()).Verifiable();

                var service = CreateService(packageService: packageService, packageOwnerRequestService: packageOwnerRequestService, useDefaultSetup: false);
                await service.AddPackageOwnerAsync(package, pendingOwner);

                packageService.Verify(x => x.AddPackageOwnerAsync(package, pendingOwner, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true), Times.Never);
            }

            [Fact]
            public async Task AddingOwnerMarksPackageVerifiedForMatchingNamespace()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = false };
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                pendingOwner.ReservedNamespaces.Add(existingNamespace);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.AddPackageOwnerAsync(package, pendingOwner);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), true, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true));
                reservedNamespaceService.Verify(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Once);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task AddingOwnerAddsPackageRegistrationToMultipleNamespaces()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = false };
                var pendingOwner = new User { Key = 100, Username = "microsoft" };
                var existingNamespace = new ReservedNamespace("microsoft.", isSharedNamespace: false, isPrefix: true);
                var existingNamespace2 = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                pendingOwner.ReservedNamespaces.Add(existingNamespace);
                pendingOwner.ReservedNamespaces.Add(existingNamespace2);

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(reservedNamespaceService: reservedNamespaceService);
                await service.AddPackageOwnerAsync(package, pendingOwner);

                reservedNamespaceService.Verify(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Exactly(2));
            }

            [Fact]
            public async Task AddingOwnerDoesNotMarkRegistrationVerifiedForAbsoluteNamespace()
            {
                var package = new PackageRegistration { Key = 2, Id = "AbsolutePackage1", IsVerified = false };
                var pendingOwner = new User { Key = 100, Username = "microsoft" };
                var existingNamespace = new ReservedNamespace("Absolute", isSharedNamespace: false, isPrefix: false);
                pendingOwner.ReservedNamespaces.Add(existingNamespace);

                var packageService = new Mock<IPackageService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService);
                await service.AddPackageOwnerAsync(package, pendingOwner);

                reservedNamespaceService.Verify(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Never);
                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var auditingService = new TestAuditingService();
                var service = CreateService(auditingService: auditingService);

                // Act
                await service.AddPackageOwnerAsync(package, pendingOwner);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageRegistrationAuditRecord>(ar =>
                    ar.Action == AuditedPackageRegistrationAction.AddOwner
                    && ar.Id == package.Id));
                Assert.Single(auditingService.Records);
            }
        }

        public class TheAddPackageOwnershipRequestAsyncMethod
        {
            [Fact]
            public async Task RequestIsAddedSuccessfully()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var auditingService = new Mock<IAuditingService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object);
                await service.AddPackageOwnershipRequestAsync(packageRegistration: package, requestingOwner: user1, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.AddPackageOwnershipRequest(package, user1,user2));
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<PackageRegistrationAuditRecord>()), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.Is<PackageRegistrationAuditRecord>(r =>
                    r.Id == "pkg42"
                    && r.RequestingOwner == "user1"
                    && r.NewOwner == "user2"
                    && r.Action == AuditedPackageRegistrationAction.AddOwnershipRequest)), Times.Once);
            }
        }

        public class TheRemovePackageOwnerAsyncMethod
        {
            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: null, requestingOwner: user1, ownerToBeRemoved: user2));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: package, requestingOwner: null, ownerToBeRemoved: user2));
            }

            [Fact]
            public async Task NullOwnerToRemoveThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: package, requestingOwner: user1, ownerToBeRemoved: null));
            }

            [Fact]
            public async Task ExistingUserIsSuccessfullyRemovedFromPackage()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", Owners = new List<User> { owner1, owner2 } };
                var packageService = new Mock<IPackageService>();

                var service = CreateService(packageService: packageService);
                await service.RemovePackageOwnerAsync(package, owner1, owner2);

                packageService.Verify(x => x.RemovePackageOwnerAsync(package, owner2, false));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var ownerToRemove = new User { Key = 100, Username = "teamawesome" };
                var package = new PackageRegistration { Key = 2, Id = "pkg42", Owners = new List<User> { owner1, ownerToRemove } };
                var auditingService = new TestAuditingService();
                var service = CreateService(auditingService: auditingService);

                // Act
                await service.RemovePackageOwnerAsync(package, owner1, ownerToRemove);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageRegistrationAuditRecord>(ar =>
                    ar.Action == AuditedPackageRegistrationAction.RemoveOwner
                    && ar.Id == package.Id));
            }

            [Fact]
            public Task RemovingNamespaceOwnerRemovesPackageVerified()
            {
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                return RemovingNamespaceOwnerRemovesPackageVerified(existingOwner1, existingOwner1);
            }

            [Fact]
            public Task RemovingNamespaceOwnerAsOrganizationAdminRemovesPackageVerified()
            {
                var existingOrganizationOwner1 = new Organization { Key = 100, Username = "microsoft" };
                var existingOrganizationOwner1Admin = new User { Key = 101, Username = "microsoftAdmin" };
                var existingMembership = new Membership { IsAdmin = true, Member = existingOrganizationOwner1Admin, Organization = existingOrganizationOwner1 };
                existingOrganizationOwner1.Members.Add(existingMembership);
                existingOrganizationOwner1Admin.Organizations.Add(existingMembership);

                return RemovingNamespaceOwnerRemovesPackageVerified(existingOrganizationOwner1, existingOrganizationOwner1Admin);
            }

            private async Task RemovingNamespaceOwnerRemovesPackageVerified(User owner, User requestingUser)
            {
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { owner } };
                owner.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                existingNamespace.Owners.Add(owner);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, requestingUser, owner);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false));
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<ReservedNamespace>(), It.IsAny<PackageRegistration>()), Times.Once);
                Assert.False(package.IsVerified);
            }

            [Fact]
            public async Task RemovingOneNamespaceOwnerDoesNotRemoveVerifiedFlag()
            {
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 101, Username = "aspnet" };
                var existingNamespace = new ReservedNamespace { Value = "microsoft.aspnet.", IsSharedNamespace = false, IsPrefix = true, Owners = new HashSet<User> { existingOwner1, existingOwner2 } };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { existingOwner1, existingOwner2 } };
                existingOwner1.ReservedNamespaces.Add(existingNamespace);
                existingOwner2.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner2, existingOwner1);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<ReservedNamespace>(), It.IsAny<PackageRegistration>()), Times.Never);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task RemovingNonNamespaceOwnerDoesNotRemoveVerifiedFlag()
            {
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 101, Username = "aspnet" };
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { existingOwner1, existingOwner2 } };
                existingOwner1.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                existingNamespace.Owners.Add(existingOwner1);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner1, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<ReservedNamespace>(), It.IsAny<PackageRegistration>()), Times.Never);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task MultipleNamespaceOwnersRemovalWorksCorrectly()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true };
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 101, Username = "aspnet" };
                var existingNamespace1 = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                var existingNamespace2 = new ReservedNamespace("microsoft.", isSharedNamespace: false, isPrefix: true);
                existingOwner1.ReservedNamespaces.Add(existingNamespace1);
                existingOwner2.ReservedNamespaces.Add(existingNamespace2);
                package.ReservedNamespaces.Add(existingNamespace1);
                package.ReservedNamespaces.Add(existingNamespace2);
                package.Owners.Add(existingOwner1);
                package.Owners.Add(existingOwner2);
                existingNamespace1.Owners.Add(existingOwner1);
                existingNamespace2.Owners.Add(existingOwner2);
                existingNamespace1.PackageRegistrations.Add(package);
                existingNamespace2.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner1, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(existingNamespace2, package), Times.Once);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task AdminCanRemoveAnyOwner()
            {
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { existingOwner1 } };
                var adminOwner = new User
                {
                    Key = 101,
                    Username = "aspnet",
                    Roles = new List<Role>
                    {
                        new Role { Name = CoreConstants.AdminRoleName }
                    }
                };
                var existingNamespace1 = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                existingOwner1.ReservedNamespaces.Add(existingNamespace1);
                package.ReservedNamespaces.Add(existingNamespace1);
                package.Owners.Add(adminOwner);
                existingNamespace1.Owners.Add(existingOwner1);
                existingNamespace1.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, adminOwner, existingOwner1);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Once);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(existingNamespace1, package), Times.Once);
                Assert.False(package.IsVerified);
            }

            [Fact]
            public async Task NormalOwnerCannotRemoveNamespaceOwner()
            {
                var namespaceOwner = new User { Key = 100, Username = "microsoft" };
                var nonNamespaceOwner = new User { Key = 101, Username = "aspnet" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { namespaceOwner, nonNamespaceOwner } };
                var existingNamespace1 = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                namespaceOwner.ReservedNamespaces.Add(existingNamespace1);
                package.ReservedNamespaces.Add(existingNamespace1);
                existingNamespace1.Owners.Add(namespaceOwner);
                existingNamespace1.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: package, requestingOwner: nonNamespaceOwner, ownerToBeRemoved: namespaceOwner));
            }

            [Fact]
            public async Task NonNamespaceOwnerCanRemoveOtherSimilarOwners()
            {
                var existingOwner1 = new User { Key = 100, Username = "owner1" };
                var existingOwner2 = new User { Key = 101, Username = "owner2" };
                var existingOwner3 = new User { Key = 102, Username = "owner3" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true, Owners = new List<User> { existingOwner1, existingOwner2, existingOwner3} };
                var existingNamespace1 = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                existingOwner3.ReservedNamespaces.Add(existingNamespace1);
                package.ReservedNamespaces.Add(existingNamespace1);
                existingNamespace1.Owners.Add(existingOwner3);
                existingNamespace1.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner1, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(existingNamespace1, package), Times.Never);
                Assert.True(package.IsVerified);
            }
        }

        public class TheDeletePackageOwnershipRequestAsyncMethod
        {
            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var user1 = new User { Key = 100, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeletePackageOwnershipRequestAsync(packageRegistration: null, newOwner: user1));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeletePackageOwnershipRequestAsync(packageRegistration: package, newOwner: null));
            }

            [Fact]
            public async Task RequestIsDeletedSuccessfully()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var auditingService = new Mock<IAuditingService>();
                var pendingRequest = new PackageOwnerRequest
                    {
                        PackageRegistration = package,
                        RequestingOwner = user1,
                        NewOwner = user2,
                        ConfirmationCode = "token"
                    };
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, useDefaultSetup: false);
                await service.DeletePackageOwnershipRequestAsync(packageRegistration: package, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(pendingRequest, true));
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<PackageRegistrationAuditRecord>()), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.Is<PackageRegistrationAuditRecord>(r =>
                    r.Id == "pkg42"
                    && r.RequestingOwner == "user1"
                    && r.NewOwner == "user2"
                    && r.Action == AuditedPackageRegistrationAction.DeleteOwnershipRequest)), Times.Once);
            }

            [Fact]
            public async Task DoesNotDeleteOrAuditIfRecordDoesNotExist()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var auditingService = new Mock<IAuditingService>();
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new PackageOwnerRequest[0]).Verifiable();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, useDefaultSetup: false);
                await service.DeletePackageOwnershipRequestAsync(packageRegistration: package, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), It.IsAny<bool>()), Times.Never);
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<PackageRegistrationAuditRecord>()), Times.Never);
            }
        }
    }
}