// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Xunit;
using System;
using System.Linq;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Collections.Generic;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.TestUtils;

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
            var dbContext = new Mock<DbContext>();
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            packageService = packageService ?? new Mock<IPackageService>();
            reservedNamespaceService = reservedNamespaceService ?? new Mock<IReservedNamespaceService>();
            packageOwnerRequestService = packageOwnerRequestService ?? new Mock<IPackageOwnerRequestService>();
            auditingService = auditingService ?? new TestAuditingService();

            if (useDefaultSetup)
            {
                entitiesContext.Setup(m => m.GetDatabase()).Returns(dbContext.Object.Database);

                packageService.Setup(x => x.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>())).Returns(Task.CompletedTask).Verifiable();
                packageService.Setup(x => x.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()))
                    .Returns((IReadOnlyCollection<PackageRegistration> list, bool isVerified) =>
                    {
                        list.ToList().ForEach(item => item.IsVerified = isVerified);
                        return Task.CompletedTask;
                    }).Verifiable();
                packageService.Setup(x => x.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>())).Returns(Task.CompletedTask).Verifiable();

                reservedNamespaceService.Setup(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>())).Verifiable();
                reservedNamespaceService.Setup(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>())).Verifiable();

                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequests(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { new PackageOwnerRequest() }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>())).Returns(Task.CompletedTask).Verifiable();
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

                packageService.Verify(x => x.AddPackageOwnerAsync(It.Is<PackageRegistration>(pr => pr == package), It.Is<User>(u => u == pendingOwner)));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequests(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>()));
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

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), It.Is<bool>(b => b == true)));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequests(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>()));
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

                reservedNamespaceService.Verify(x => x.AddPackageRegistrationToNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Between(2, 2, Range.Inclusive));
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
                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>()), Times.Never);
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
            }
        }

        public class TheAddPackageOwnershipRequestAsyncMethod
        {
            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var user1 = new User { Key = 100, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequestAsync(packageRegistration: null, requestingOwner: user1, newOwner: user2));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequestAsync(packageRegistration: package, requestingOwner: null, newOwner: user2));
            }

            [Fact]
            public async Task NullNewOwnerThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequestAsync(packageRegistration: package, requestingOwner: user1, newOwner: null));
            }

            [Fact]
            public async Task RequestIsAddedSuccessfully()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService);
                await service.AddPackageOwnershipRequestAsync(packageRegistration: package, requestingOwner: user1, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.AddPackageOwnershipRequest(It.Is<PackageRegistration>(pr => pr == package), It.Is<User>(u1 => u1 == user1), It.Is<User>(u2 => u2 == user2)));
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
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: null, user: user1));
            }

            [Fact]
            public async Task NullUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.RemovePackageOwnerAsync(packageRegistration: package, user: null));
            }

            [Fact]
            public async Task ExistingUserIsSuccessfullyRemovedFromPackage()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", Owners = new List<User> { owner1, owner2 } };
                var packageService = new Mock<IPackageService>();

                var service = CreateService(packageService: packageService);
                await service.RemovePackageOwnerAsync(package, owner1);

                packageService.Verify(x => x.RemovePackageOwnerAsync(It.Is<PackageRegistration>(pr => pr == package), It.Is<User>(u => u == owner1)));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var ownerToRemove = new User { Key = 100, Username = "teamawesome" };
                var auditingService = new TestAuditingService();
                var service = CreateService(auditingService: auditingService);

                // Act
                await service.RemovePackageOwnerAsync(package, ownerToRemove);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageRegistrationAuditRecord>(ar =>
                    ar.Action == AuditedPackageRegistrationAction.RemoveOwner
                    && ar.Id == package.Id));
            }

            [Fact]
            public async Task RemovingNamespaceOwnerRemovesPackageVerified()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true };
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                existingOwner1.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                package.Owners.Add(existingOwner1);
                existingNamespace.Owners.Add(existingOwner1);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner1);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), It.Is<bool>(b => b == false)));
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Once);
                Assert.False(package.IsVerified);
            }

            [Fact]
            public async Task RemovingOneNamespaceOwnerDoesNotRemoveVerifiedFlag()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true };
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 100, Username = "aspnet" };
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                existingOwner1.ReservedNamespaces.Add(existingNamespace);
                existingOwner2.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                package.Owners.Add(existingOwner1);
                package.Owners.Add(existingOwner2);
                existingNamespace.Owners.Add(existingOwner1);
                existingNamespace.Owners.Add(existingOwner2);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner1);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), It.Is<bool>(b => b == false)), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Never);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task RemovingNonNamespaceOwnerDoesNotRemoveVerifiedFlag()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true };
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 100, Username = "aspnet" };
                var existingNamespace = new ReservedNamespace("microsoft.aspnet.", isSharedNamespace: false, isPrefix: true);
                existingOwner1.ReservedNamespaces.Add(existingNamespace);
                package.ReservedNamespaces.Add(existingNamespace);
                package.Owners.Add(existingOwner1);
                package.Owners.Add(existingOwner2);
                existingNamespace.Owners.Add(existingOwner1);
                existingNamespace.PackageRegistrations.Add(package);

                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var reservedNamespaceService = new Mock<IReservedNamespaceService>();

                var service = CreateService(packageService: packageService, reservedNamespaceService: reservedNamespaceService, packageOwnerRequestService: packageOwnerRequestService);
                await service.RemovePackageOwnerAsync(package, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), It.Is<bool>(b => b == false)), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.IsAny<string>(), It.IsAny<PackageRegistration>()), Times.Never);
                Assert.True(package.IsVerified);
            }

            [Fact]
            public async Task MultipleNamespaceOwnersRemovalWorksCorrectly()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", IsVerified = true };
                var existingOwner1 = new User { Key = 100, Username = "microsoft" };
                var existingOwner2 = new User { Key = 100, Username = "aspnet" };
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
                await service.RemovePackageOwnerAsync(package, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), It.Is<bool>(b => b == false)), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(It.Is<string>(value => value == existingNamespace2.Value), It.Is<PackageRegistration>(prop => prop == package)), Times.Once);
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
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeletePackageOwnershipRequestAsync(packageRegistration: null, user: user1));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeletePackageOwnershipRequestAsync(packageRegistration: package, user: null));
            }

            [Fact]
            public async Task RequestIsDeletedSuccessfully()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var pendingRequest = new PackageOwnerRequest
                    {
                        PackageRegistration = package,
                        NewOwner = user1,
                        ConfirmationCode = "token"
                    };
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequests(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>())).Returns(Task.CompletedTask).Verifiable();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, useDefaultSetup: false);
                await service.DeletePackageOwnershipRequestAsync(packageRegistration: package, user: user1);
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.Is<PackageOwnerRequest>(req => req == pendingRequest)));
            }
        }
    }
}