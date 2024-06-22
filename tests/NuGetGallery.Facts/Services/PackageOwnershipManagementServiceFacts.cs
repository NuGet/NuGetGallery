// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Mail.Messages;
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
            Mock<IUrlHelper> urlHelper = null,
            Mock<IAppConfiguration> appConfiguration = null,
            Mock<IMessageService> messageService = null,
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
            urlHelper = urlHelper ?? new Mock<IUrlHelper>();
            appConfiguration = appConfiguration ?? new Mock<IAppConfiguration>();
            messageService = messageService ?? new Mock<IMessageService>();

            if (useDefaultSetup)
            {
                packageService
                    .Setup(x => x.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<bool>()))
                    .Callback<PackageRegistration, User, bool>((pr, u, c) => pr.Owners.Add(u))
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
                    .Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()))
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

                urlHelper.SetReturnsDefault<string>("https://some-url");
            }

            var packageOwnershipManagementService = new Mock<PackageOwnershipManagementService>(
                entitiesContext.Object,
                packageService.Object,
                reservedNamespaceService.Object,
                packageOwnerRequestService.Object,
                auditingService,
                urlHelper.Object,
                appConfiguration.Object,
                messageService.Object);

            return packageOwnershipManagementService.Object;
        }

        public class TheAddPackageOwnerWithMessagesAsyncMethod : TheAddPackageOwnerAsyncMethodFacts
        {
            [Fact]
            public async Task SendsMessagesToAllOwners()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var existingOwner = new User { Key = 99, Username = "microsoft" };
                package.Owners.Add(existingOwner);
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();

                var service = CreateService(messageService: messageService);
                await AddPackageOwnerAsync(service, package, pendingOwner);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Exactly(2));
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnerAddedMessage>(m => m.PackageUrl == "https://some-url" && m.NewOwner == pendingOwner && m.ToUser == existingOwner),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnerAddedMessage>(m => m.PackageUrl == "https://some-url" && m.NewOwner == pendingOwner && m.ToUser == pendingOwner),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
            }

            [Fact]
            public async Task SendsAllMessagesEvenIfOneFails()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                package.Owners.Add(new User { Key = 96, Username = "microsoftA" });
                package.Owners.Add(new User { Key = 97, Username = "microsoftB" });
                package.Owners.Add(new User { Key = 98, Username = "microsoftC" });
                package.Owners.Add(new User { Key = 99, Username = "microsoftD" });
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();
                messageService
                    .Setup(x => x.SendMessageAsync(It.Is<PackageOwnerAddedMessage>(m => m.ToUser.Username == "microsoftC"), It.IsAny<bool>(), It.IsAny<bool>()))
                    .ThrowsAsync(new InvalidOperationException("The message could not be sent."));

                var service = CreateService(messageService: messageService);

                await Assert.ThrowsAsync<InvalidOperationException>(() => AddPackageOwnerAsync(service, package, pendingOwner));

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Exactly(5));
            }

            protected override async Task AddPackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User user)
            {
                await service.AddPackageOwnerWithMessagesAsync(packageRegistration, user);
            }
        }

        public class TheAddPackageOwnerAsyncMethod : TheAddPackageOwnerAsyncMethodFacts
        {
            [Fact]
            public async Task SendsNoMessages()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();

                var service = CreateService(messageService: messageService);
                await AddPackageOwnerAsync(service, package, pendingOwner);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            protected override async Task AddPackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User user)
            {
                await service.AddPackageOwnerAsync(packageRegistration, user);
            }
        }

        public abstract class TheAddPackageOwnerAsyncMethodFacts
        {
            protected abstract Task AddPackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User user);

            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await AddPackageOwnerAsync(service, packageRegistration: null, user: testUsers.First()));
            }

            [Fact]
            public async Task NullUserThrowsException()
            {
                var service = CreateService();
                var testPackageRegistrations = ReservedNamespaceServiceTestData.GetRegistrations();
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await AddPackageOwnerAsync(service, packageRegistration: testPackageRegistrations.First(), user: null));
            }

            [Fact]
            public async Task NewOwnerIsAddedSuccessfullyToTheRegistration()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var pendingOwner = new User { Key = 100, Username = "aspnet" };
                var packageService = new Mock<IPackageService>();
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();

                var service = CreateService(packageService: packageService, packageOwnerRequestService: packageOwnerRequestService);
                await AddPackageOwnerAsync(service, package, pendingOwner);

                packageService.Verify(x => x.AddPackageOwnerAsync(package, pendingOwner, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
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
                await AddPackageOwnerAsync(service, package, pendingOwner);

                packageService.Verify(x => x.AddPackageOwnerAsync(package, pendingOwner, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
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
                await AddPackageOwnerAsync(service, package, pendingOwner);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), true, true));
                packageOwnerRequestService.Verify(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>()));
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
                await AddPackageOwnerAsync(service, package, pendingOwner);

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
                await AddPackageOwnerAsync(service, package, pendingOwner);

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
                await AddPackageOwnerAsync(service, package, pendingOwner);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageRegistrationAuditRecord>(ar =>
                    ar.Action == AuditedPackageRegistrationAction.AddOwner
                    && ar.Id == package.Id));
                Assert.Single(auditingService.Records);
            }
        }

        public class TheAddPackageOwnershipRequestWithMessagesAsyncMethod : TheAddPackageOwnershipRequestAsyncMethodFacts
        {
            [Fact]
            public async Task SendsMessagesToAllOwners()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var requestingOwner = new User { Key = 99, Username = "MicrosoftA" };
                var existingOwner = new User { Key = 100, Username = "MicrosoftB" };
                package.Owners.Add(requestingOwner);
                package.Owners.Add(existingOwner);
                var newOwner = new User { Key = 101, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();

                var service = CreateService(messageService: messageService);
                await AddPackageOwnershipRequestAsync(service, package, requestingOwner, newOwner);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Exactly(3));
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnershipRequestInitiatedMessage>(m => m.NewOwner == newOwner && m.ReceivingOwner == requestingOwner && m.RequestingOwner == m.RequestingOwner),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnershipRequestInitiatedMessage>(m => m.NewOwner == newOwner && m.ReceivingOwner == existingOwner && m.RequestingOwner == m.RequestingOwner),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnershipRequestMessage>(m => m.ToUser == newOwner && m.FromUser == requestingOwner),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
            }

            [Fact]
            public async Task SendsAllMessagesEvenIfOneFails()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                package.Owners.Add(new User { Key = 96, Username = "microsoftA" });
                package.Owners.Add(new User { Key = 97, Username = "microsoftB" });
                package.Owners.Add(new User { Key = 98, Username = "microsoftC" });
                var requestingOwner = new User { Key = 100, Username = "MicrosoftD" };
                package.Owners.Add(requestingOwner);
                var newOwner = new User { Key = 100, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();
                messageService
                    .Setup(x => x.SendMessageAsync(It.Is<PackageOwnershipRequestInitiatedMessage>(m => m.ReceivingOwner.Username == "microsoftC"), It.IsAny<bool>(), It.IsAny<bool>()))
                    .ThrowsAsync(new InvalidOperationException("The message could not be sent."));

                var service = CreateService(messageService: messageService);

                await Assert.ThrowsAsync<InvalidOperationException>(() => AddPackageOwnershipRequestAsync(service, package, requestingOwner, newOwner));

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Exactly(5));
            }

            protected override async Task AddPackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner)
            {
                await service.AddPackageOwnershipRequestWithMessagesAsync(packageRegistration, requestingOwner, newOwner, message: string.Empty);
            }
        }

        public class TheAddPackageOwnershipRequestAsyncMethod : TheAddPackageOwnershipRequestAsyncMethodFacts
        {
            [Fact]
            public async Task SendsNoMessages()
            {
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1" };
                var requestingOwner = new User { Key = 99, Username = "Microsoft" };
                package.Owners.Add(requestingOwner);
                var newOwner = new User { Key = 100, Username = "aspnet" };
                var messageService = new Mock<IMessageService>();

                var service = CreateService(messageService: messageService);
                await AddPackageOwnershipRequestAsync(service, package, requestingOwner, newOwner);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            protected override async Task AddPackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner)
            {
                await service.AddPackageOwnershipRequestAsync(packageRegistration, requestingOwner, newOwner);
            }
        }

        public abstract class TheAddPackageOwnershipRequestAsyncMethodFacts
        {
            protected abstract Task AddPackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner);

            [Fact]
            public async Task RejectsNullPackageRegistration()
            {
                var service = CreateService();
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => AddPackageOwnershipRequestAsync(service, packageRegistration: null, requestingOwner: user1, newOwner: user2));
                Assert.Equal("packageRegistration", ex.ParamName);
            }

            [Fact]
            public async Task RejectsNullRequestingOwner()
            {
                var service = CreateService();
                var packageRegistrion = new PackageRegistration();
                var user1 = new User { Key = 101, Username = "user1" };
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => AddPackageOwnershipRequestAsync(service, packageRegistrion, requestingOwner: null, newOwner: user1));
                Assert.Equal("requestingOwner", ex.ParamName);
            }

            [Fact]
            public async Task RejectsNullNewOwner()
            {
                var service = CreateService();
                var packageRegistrion = new PackageRegistration();
                var user1 = new User { Key = 101, Username = "user1" };
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => AddPackageOwnershipRequestAsync(service, packageRegistrion, requestingOwner: user1, newOwner: null));
                Assert.Equal("newOwner", ex.ParamName);
            }

            [Fact]
            public async Task RequestIsAddedSuccessfully()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                var packageOwnerRequestService = new Mock<IPackageOwnerRequestService>();
                var auditingService = new Mock<IAuditingService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object);
                await AddPackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.AddPackageOwnershipRequest(package, user1,user2));
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<PackageRegistrationAuditRecord>()), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.Is<PackageRegistrationAuditRecord>(r =>
                    r.Id == "pkg42"
                    && r.RequestingOwner == "user1"
                    && r.NewOwner == "user2"
                    && r.Action == AuditedPackageRegistrationAction.AddOwnershipRequest)), Times.Once);
            }
        }

        public class TheRemovePackageOwnerWithMessagesAsyncMethod : TheRemovePackageOwnerAsyncMethodFacts
        {
            [Fact]
            public async Task SendsMessage()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", Owners = new List<User> { owner1, owner2 } };
                var packageService = new Mock<IPackageService>();
                var messageService = new Mock<IMessageService>();

                var service = CreateService(packageService: packageService, messageService: messageService);
                await RemovePackageOwnerAsync(service, package, owner1, owner2);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Once);
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnerRemovedMessage>(m => m.FromUser == owner1 && m.ToUser == owner2),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
            }

            [Fact]
            public async Task AllowsNamespaceOwnershipToBeChecked()
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
                await Assert.ThrowsAsync<InvalidOperationException>(() => RemovePackageOwnerAsync(service, package, nonNamespaceOwner, namespaceOwner));

                Assert.Contains(namespaceOwner, package.Owners);
                Assert.Contains(nonNamespaceOwner, package.Owners);
            }

            [Fact]
            public async Task AllowsNamespaceOwnershipToBeSkipped()
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
                await service.RemovePackageOwnerWithMessagesAsync(package, nonNamespaceOwner, namespaceOwner, requireNamespaceOwnership: false);

                Assert.DoesNotContain(namespaceOwner, package.Owners);
                Assert.Contains(nonNamespaceOwner, package.Owners);
            }

            protected async override Task RemovePackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved)
            {
                await service.RemovePackageOwnerWithMessagesAsync(packageRegistration, requestingOwner, ownerToBeRemoved, requireNamespaceOwnership: true);
            }
        }

        public class TheRemovePackageOwnerAsyncMethod : TheRemovePackageOwnerAsyncMethodFacts
        {
            [Fact]
            public async Task SendsNoMessages()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", Owners = new List<User> { owner1, owner2 } };
                var packageService = new Mock<IPackageService>();
                var messageService = new Mock<IMessageService>();

                var service = CreateService(packageService: packageService, messageService: messageService);
                await RemovePackageOwnerAsync(service, package, owner1, owner2);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            protected async override Task RemovePackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved)
            {
                await service.RemovePackageOwnerAsync(packageRegistration, requestingOwner, ownerToBeRemoved);
            }
        }

        public abstract class TheRemovePackageOwnerAsyncMethodFacts
        {
            protected abstract Task RemovePackageOwnerAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved);

            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await RemovePackageOwnerAsync(service, packageRegistration: null, requestingOwner: user1, ownerToBeRemoved: user2));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await RemovePackageOwnerAsync(service, packageRegistration: package, requestingOwner: null, ownerToBeRemoved: user2));
            }

            [Fact]
            public async Task NullOwnerToRemoveThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await RemovePackageOwnerAsync(service, packageRegistration: package, requestingOwner: user1, ownerToBeRemoved: null));
            }

            [Fact]
            public async Task ExistingUserIsSuccessfullyRemovedFromPackage()
            {
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Key = 2, Id = "Microsoft.Aspnet.Package1", Owners = new List<User> { owner1, owner2 } };
                var packageService = new Mock<IPackageService>();

                var service = CreateService(packageService: packageService);
                await RemovePackageOwnerAsync(service, package, owner1, owner2);

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
                await RemovePackageOwnerAsync(service, package, owner1, ownerToRemove);

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
                await RemovePackageOwnerAsync(service, package, requestingUser, owner);

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
                await RemovePackageOwnerAsync(service, package, existingOwner2, existingOwner1);

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
                await RemovePackageOwnerAsync(service, package, existingOwner1, existingOwner2);

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
                await RemovePackageOwnerAsync(service, package, existingOwner1, existingOwner2);

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
                await RemovePackageOwnerAsync(service, package, adminOwner, existingOwner1);

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
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await RemovePackageOwnerAsync(service, packageRegistration: package, requestingOwner: nonNamespaceOwner, ownerToBeRemoved: namespaceOwner));
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
                await RemovePackageOwnerAsync(service, package, existingOwner1, existingOwner2);

                packageService.Verify(x => x.UpdatePackageVerifiedStatusAsync(It.Is<IReadOnlyCollection<PackageRegistration>>(pr => pr.First() == package), false, false), Times.Never);
                reservedNamespaceService.Verify(x => x.RemovePackageRegistrationFromNamespace(existingNamespace1, package), Times.Never);
                Assert.True(package.IsVerified);
            }
        }

        public class TheCancelPackageOwnershipRequestWithMessagesAsyncMethod : TheDeletePackageOwnershipRequestAsyncMethodFacts
        {
            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: null, newOwner: user1));
            }

            [Fact]
            public async Task SendsMessage()
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
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                var messageService = new Mock<IMessageService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, messageService: messageService, useDefaultSetup: false);
                await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Once);
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnershipRequestCanceledMessage>(m => m.NewOwner == user2 && m.RequestingOwner == user1),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
            }

            protected override async Task DeletePackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner)
            {
                await service.CancelPackageOwnershipRequestWithMessagesAsync(packageRegistration, requestingOwner, newOwner);
            }
        }

        public class TheDeclinePackageOwnershipRequestWithMessagesAsyncMethod : TheDeletePackageOwnershipRequestAsyncMethodFacts
        {
            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: null, newOwner: user1));
            }

            [Fact]
            public async Task SendsMessage()
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
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                var messageService = new Mock<IMessageService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, messageService: messageService, useDefaultSetup: false);
                await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Once);
                messageService.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageOwnershipRequestDeclinedMessage>(m => m.NewOwner == user2 && m.RequestingOwner == user1),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()));
            }

            protected override async Task DeletePackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner)
            {
                await service.DeclinePackageOwnershipRequestWithMessagesAsync(packageRegistration, requestingOwner, newOwner);
            }
        }

        public class TheDeletePackageOwnershipRequestAsyncMethod : TheDeletePackageOwnershipRequestAsyncMethodFacts
        {
            [Fact]
            public async Task SendsNoMessages()
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
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                var messageService = new Mock<IMessageService>();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, messageService: messageService, useDefaultSetup: false);
                await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);

                messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            protected override async Task DeletePackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner)
            {
                await service.DeletePackageOwnershipRequestAsync(packageRegistration, newOwner);
            }
        }

        public abstract class TheDeletePackageOwnershipRequestAsyncMethodFacts
        {
            protected abstract Task DeletePackageOwnershipRequestAsync(PackageOwnershipManagementService service, PackageRegistration packageRegistration, User requestingOwner, User newOwner);

            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var user1 = new User { Key = 101, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await DeletePackageOwnershipRequestAsync(service, packageRegistration: null, requestingOwner: user2, newOwner: user1));
            }

            [Fact]
            public async Task NullNewUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: null));
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
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(new[] { pendingRequest }).Verifiable();
                packageOwnerRequestService.Setup(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), true)).Returns(Task.CompletedTask).Verifiable();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, useDefaultSetup: false);
                await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);
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
                packageOwnerRequestService.Setup(x => x.GetPackageOwnershipRequestsWithUsers(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>())).Returns(Array.Empty<PackageOwnerRequest>()).Verifiable();
                var service = CreateService(packageOwnerRequestService: packageOwnerRequestService, auditingService: auditingService.Object, useDefaultSetup: false);
                await DeletePackageOwnershipRequestAsync(service, packageRegistration: package, requestingOwner: user1, newOwner: user2);
                packageOwnerRequestService.Verify(x => x.DeletePackageOwnershipRequest(It.IsAny<PackageOwnerRequest>(), It.IsAny<bool>()), Times.Never);
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<PackageRegistrationAuditRecord>()), Times.Never);
            }
        }
    }
}