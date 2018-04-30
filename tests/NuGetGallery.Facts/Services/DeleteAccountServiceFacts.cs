﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Security;
using Xunit;
using Moq;

namespace NuGetGallery.Services
{
    public class DeleteAccountServiceFacts
    {
        public class TheDeleteGalleryUserAccountAsyncMethod
        {
            private static int Key = -1;

            [Fact]
            public async Task NullUser()
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteAccountAsync(
                    null, 
                    new User("AdminUser") { Key = Key++ },
                    commitAsTransaction: false,
                    signature: "signature",
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans));
            }

            [Fact]
            public async Task NullAdmin()
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteAccountAsync(
                    new User("TestUser") { Key = Key++ },
                    null,
                    commitAsTransaction: false,
                    signature: "signature",
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans));
            }

            /// <summary>
            /// The action to delete a deleted user will be a no-op.
            /// </summary>
            [Fact]
            public async Task DeleteDeletedUser()
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                testUser.IsDeleted = true;
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var signature = "Hello";
                var result = await deleteAccountService.
                    DeleteAccountAsync(userToBeDeleted: testUser,
                                                userToExecuteTheDelete: testUser,
                                                commitAsTransaction: false,
                                                signature: signature,
                                                orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);
                string expected = $"The account:{testUser.Username} was already deleted. No action was performed.";
                Assert.Equal<string>(expected, result.Description);
            }

            /// <summary>
            /// One user with one package that has one namespace reserved and one security policy.
            /// After the account deletion:
            /// The user data(for example the email address) will be cleaned
            /// The package will be unlisted.
            /// The user will have the policies removed.
            /// The namespace will be unassigned from the user.
            /// The information about the deletion will be saved.
            /// </summary>
            [Fact]
            public async Task DeleteHappyUser()
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testUserOrganizations = testUser.Organizations.ToList();
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var signature = "Hello";
                await deleteAccountService.
                    DeleteAccountAsync(userToBeDeleted: testUser,
                                                userToExecuteTheDelete: testUser,
                                                commitAsTransaction: false,
                                                signature: signature,
                                                orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);
                
                Assert.Empty(registration.Owners);
                Assert.Empty(testUser.SecurityPolicies);
                Assert.Empty(testUser.ReservedNamespaces);
                Assert.Equal(false, registration.Packages.ElementAt(0).Listed);
                Assert.Null(testUser.EmailAddress);
                Assert.Equal(1, testableService.DeletedAccounts.Count());
                Assert.Equal(signature, testableService.DeletedAccounts.ElementAt(0).Signature);
                Assert.Equal(1, testableService.SupportRequests.Count());
                Assert.Empty(testableService.PackageOwnerRequests);
                Assert.Equal(1, testableService.AuditService.Records.Count());
                Assert.Null(testUser.OrganizationMigrationRequest);
                Assert.Empty(testUser.OrganizationMigrationRequests);
                Assert.Empty(testUser.OrganizationRequests);

                Assert.Empty(testUser.Organizations);
                foreach (var testUserOrganization in testUserOrganizations)
                {
                    var notDeletedMembers = testUserOrganization.Organization.Members.Where(m => m.Member != testUser);
                    if (!notDeletedMembers.Any())
                    {
                        Assert.Contains(testUserOrganization.Organization, testableService.DeletedUsers);
                    }
                    else
                    {
                        Assert.True(notDeletedMembers.Any(m => m.IsAdmin));
                    }
                }
                
                var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
            }

            [Fact]
            public async Task WhenUserIsNotConfirmedTheUserRecordIsDeleted()
            {
                //Arange
                User testUser = new User("TestsUser") { Key = Key++, UnconfirmedEmailAddress = "user@test.com" };
                var testableService = new DeleteAccountTestService(testUser);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Act
                var status = await deleteAccountService.DeleteAccountAsync(userToBeDeleted: testUser,
                                                userToExecuteTheDelete: testUser,
                                                commitAsTransaction: false,
                                                signature: testUser.Username,
                                                orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                //Assert
                Assert.True(status.Success);
                Assert.Null(testableService.User);
                Assert.Equal(1, testableService.AuditService.Records.Count);
                var deleteAccountAuditRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.NotNull(deleteAccountAuditRecord);
                Assert.Equal(testUser.Username, deleteAccountAuditRecord.AdminUsername);
                Assert.Equal(testUser.Username, deleteAccountAuditRecord.Username);
                Assert.Equal(DeleteAccountAuditRecord.ActionStatus.Success, deleteAccountAuditRecord.Status);
            }

            [Fact]
            public async Task DeleteConfirmedOrganization()
            {
                // Arrange
                var member = new User("testUser") { Key = Key++ };
                var organization = new Organization("testOrganization") { Key = Key++, EmailAddress = "org@test.com" };

                var membership = new Membership() { Organization = organization, Member = member };
                member.Organizations.Add(membership);
                organization.Members.Add(membership);

                var requestedMember = new User("testRequestedMember") { Key = Key++ };
                var memberRequest = new MembershipRequest() { Organization = organization, NewMember = requestedMember };
                requestedMember.OrganizationRequests.Add(memberRequest);
                organization.MemberRequests.Add(memberRequest);

                PackageRegistration registration = new PackageRegistration();
                registration.Owners.Add(organization);

                Package p = new Package()
                {
                    Description = "TestPackage",
                    Key = 1
                };
                p.PackageRegistration = registration;
                registration.Packages.Add(p);

                var testableService = new DeleteAccountTestService(organization, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var status = await deleteAccountService.
                    DeleteAccountAsync(
                        organization,
                        member,
                        commitAsTransaction: false,
                        orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.KeepOrphans);

                // Assert
                Assert.True(status.Success);
                Assert.Null(organization.EmailAddress);
                Assert.Equal(0, registration.Owners.Count());
                Assert.Equal(0, organization.SecurityPolicies.Count());
                Assert.Equal(0, organization.ReservedNamespaces.Count());
                Assert.Equal(1, testableService.DeletedAccounts.Count());
                Assert.Equal(1, testableService.SupportRequests.Count);
                Assert.Equal(0, testableService.PackageOwnerRequests.Count);
                Assert.Equal(1, testableService.AuditService.Records.Count);
                var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
            }

            [Fact]
            public async Task DeleteUnconfirmedOrganization()
            {
                // Arrange
                var member = new User("testUser") { Key = Key++ };
                var organization = new Organization("testOrganization") { Key = Key++ };

                var membership = new Membership() { Organization = organization, Member = member };
                member.Organizations.Add(membership);
                organization.Members.Add(membership);

                var requestedMember = new User("testRequestedMember") { Key = Key++ };
                var memberRequest = new MembershipRequest() { Organization = organization, NewMember = requestedMember };
                requestedMember.OrganizationRequests.Add(memberRequest);
                organization.MemberRequests.Add(memberRequest);

                PackageRegistration registration = new PackageRegistration();
                registration.Owners.Add(organization);

                Package p = new Package()
                {
                    Description = "TestPackage",
                    Key = 1
                };
                p.PackageRegistration = registration;
                registration.Packages.Add(p);

                var testableService = new DeleteAccountTestService(organization, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var status = await deleteAccountService.
                    DeleteAccountAsync(
                        organization,
                        member,
                        commitAsTransaction: false,
                        orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.KeepOrphans);

                // Assert
                Assert.True(status.Success);
                Assert.Null(testableService.User);
                Assert.Equal(0, registration.Owners.Count());
                Assert.Equal(0, organization.SecurityPolicies.Count());
                Assert.Equal(0, organization.ReservedNamespaces.Count());
                Assert.Equal(0, testableService.DeletedAccounts.Count());
                Assert.Equal(1, testableService.SupportRequests.Count);
                Assert.Equal(0, testableService.PackageOwnerRequests.Count);
                Assert.Equal(1, testableService.AuditService.Records.Count);
                var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
            }

            private static User CreateTestData(ref PackageRegistration registration)
            {
                var testUser = new User("TestUser") { Key = Key++ };
                testUser.EmailAddress = "user@test.com";

                AddOrganizationMigrationRequest(testUser);
                AddOrganizationMigrationRequests(testUser);
                AddOrganizationRequests(testUser, false);
                AddOrganizationRequests(testUser, true);

                foreach (var isAdmin in new[] { false, true })
                {
                    foreach (var hasAdminMember in new[] { false, true })
                    {
                        foreach (var hasCollaboratorMember in new[] { false, true })
                        {
                            AddOrganization(testUser, isAdmin, hasAdminMember, hasCollaboratorMember);
                        }
                    }
                }

                registration = new PackageRegistration();
                registration.Owners.Add(testUser);

                var p = new Package()
                {
                    Description = "TestPackage",
                    Key = 1
                };
                p.PackageRegistration = registration;
                registration.Packages.Add(p);
                return testUser;
            }

            private static void AddOrganizationMigrationRequest(User testUser)
            {
                var testOrganizationAdmin = new User("TestOrganizationAdmin") { Key = Key++ };

                var request = new OrganizationMigrationRequest { AdminUser = testOrganizationAdmin, NewOrganization = testUser };
                testUser.OrganizationMigrationRequest = request;
                testOrganizationAdmin.OrganizationMigrationRequests.Add(request);
            }

            private static void AddOrganizationMigrationRequests(User testUser)
            {
                var testOrganization = new Organization("testOrganization") { Key = Key++ };

                var request = new OrganizationMigrationRequest { AdminUser = testUser, NewOrganization = testOrganization };
                testOrganization.OrganizationMigrationRequest = request;
                testUser.OrganizationMigrationRequests.Add(request);
            }

            private static void AddOrganizationRequests(User testUser, bool isAdmin)
            {
                var testOrganization = new Organization("testOrganization") { Key = Key++ };

                var request = new MembershipRequest { IsAdmin = isAdmin, NewMember = testUser, Organization = testOrganization };
                testOrganization.MemberRequests.Add(request);
                testUser.OrganizationRequests.Add(request);
            }

            private static void AddOrganization(User testUser, bool isAdmin, bool hasAdminMember, bool hasCollaboratorMember)
            {
                var testOrganization = new Organization("testOrganization") { Key = Key++ };

                AddMemberToOrganization(testOrganization, testUser, isAdmin);

                if (hasAdminMember)
                {
                    var adminUser = new User("testAdministrator") { Key = Key++ };
                    AddMemberToOrganization(testOrganization, adminUser, isAdmin);
                }

                if (hasCollaboratorMember)
                {
                    var collaboratorUser = new User("testCollaborator") { Key = Key++ };
                    AddMemberToOrganization(testOrganization, collaboratorUser, isAdmin);
                }
            }

            private static void AddMemberToOrganization(Organization organization, User user, bool isAdmin)
            {
                var membership = new Membership { IsAdmin = isAdmin, Organization = organization, Member = user };
                organization.Members.Add(membership);
                user.Organizations.Add(membership);
            }
        }

        public class DeleteAccountTestService
        {
            private const string SubscriptionName = "SecPolicySubscription";
            private User _user = null;
            private static ReservedNamespace _reserverdNamespace = new ReservedNamespace("Ns1", false, false);
            private Credential _credential = new Credential("CredType", "CredValue");
            private UserSecurityPolicy _securityPolicy = new UserSecurityPolicy("PolicyName", SubscriptionName);
            private PackageRegistration _userPackagesRegistration = null;
            private ICollection<Package> _userPackages;

            public List<AccountDelete> DeletedAccounts = new List<AccountDelete>();
            public List<User> DeletedUsers = new List<User>();
            public List<Issue> SupportRequests = new List<Issue>();
            public List<PackageOwnerRequest> PackageOwnerRequests = new List<PackageOwnerRequest>();
            public FakeAuditingService AuditService = new FakeAuditingService();

            public DeleteAccountTestService(User user)
            {
                _user = user;
                _userPackages = new List<Package>();

                AuditService = new FakeAuditingService();
            }

            public DeleteAccountTestService(User user, PackageRegistration userPackagesRegistration)
            {
                _user = user;
                _user.ReservedNamespaces.Add(_reserverdNamespace);
                _user.Credentials.Add(_credential);
                _user.SecurityPolicies.Add(_securityPolicy);
                _userPackagesRegistration = userPackagesRegistration;
                _userPackages = userPackagesRegistration.Packages;

                SupportRequests.Add(new Issue()
                {
                    CreatedBy = user.Username,
                    Key = 1,
                    IssueTitle = Strings.AccountDelete_SupportRequestTitle,
                    OwnerEmail = user.EmailAddress,
                    IssueStatusId = IssueStatusKeys.New,
                    HistoryEntries = new List<History>() { new History() { EditedBy = user.Username, IssueId = 1, Key = 1, IssueStatusId = IssueStatusKeys.New } }
                });

                SupportRequests.Add(new Issue()
                {
                    CreatedBy = $"{user.Username}_second",
                    Key = 2,
                    IssueTitle = "Second",
                    OwnerEmail = "random",
                    IssueStatusId = IssueStatusKeys.New,
                    HistoryEntries = new List<History>() { new History() { EditedBy = $"{user.Username}_second", IssueId = 2, Key = 2, IssueStatusId = IssueStatusKeys.New } }
                });

                PackageOwnerRequests.Add(new PackageOwnerRequest()
                {
                    PackageRegistration = new PackageRegistration() { Id = $"{user.Username}_first" },
                    NewOwner = _user
                });

                PackageOwnerRequests.Add(new PackageOwnerRequest()
                {
                    PackageRegistration = new PackageRegistration() { Id = $"{user.Username}_second" },
                    NewOwner = _user
                });
            }

            public DeleteAccountTestService()
            {
            }

            public DeleteAccountService GetDeleteAccountService()
            {
                return new DeleteAccountService(SetupAccountDeleteRepository().Object,
                    SetupUserRepository().Object,
                    SetupEntitiesContext().Object,
                    SetupPackageService().Object,
                    SetupPackageOwnershipManagementService().Object,
                    SetupReservedNamespaceService().Object,
                    SetupSecurityPolicyService().Object,
                    new TestableAuthService(),
                    SetupSupportRequestService().Object,
                    AuditService);
            }

            public class FakeAuditingService : IAuditingService
            {
                public List<AuditRecord> Records = new List<AuditRecord>();

                public Task SaveAuditRecordAsync(AuditRecord record)
                {
                    Records.Add(record);
                    return Task.FromResult(true);
                }
            }

            public User User
            {
                get { return _user; }
            }

            private class TestableAuthService : AuthenticationService
            {
                public TestableAuthService() : base()
                { }

                public override async Task AddCredential(User user, Credential credential)
                {
                    await Task.Yield();
                    user.Credentials.Add(credential);
                }

                public override async Task RemoveCredential(User user, Credential credential)
                {
                    await Task.Yield();
                    user.Credentials.Remove(credential);
                }
            }

            private Mock<IEntitiesContext> SetupEntitiesContext()
            {
                var mockContext = new Mock<IEntitiesContext>();
                var dbContext = new Mock<DbContext>();
                mockContext.Setup(m => m.GetDatabase()).Returns(new DatabaseWrapper(dbContext.Object.Database));
                return mockContext;
            }

            private Mock<IReservedNamespaceService> SetupReservedNamespaceService()
            {
                var namespaceService = new Mock<IReservedNamespaceService>();
                if (_user != null)
                {
                    namespaceService.Setup(m => m.DeleteOwnerFromReservedNamespaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                                    .Returns(Task.CompletedTask)
                                    .Callback(() => _user.ReservedNamespaces.Remove(_reserverdNamespace));
                }

                return namespaceService;
            }

            private Mock<ISecurityPolicyService> SetupSecurityPolicyService()
            {
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                if (_user != null)
                {
                    securityPolicyService.Setup(m => m.UnsubscribeAsync(_user, SubscriptionName))
                                         .Returns(Task.CompletedTask)
                                         .Callback(() => _user.SecurityPolicies.Remove(_securityPolicy));
                }

                return securityPolicyService;
            }

            private Mock<IEntityRepository<AccountDelete>> SetupAccountDeleteRepository()
            {
                var accountDeleteRepository = new Mock<IEntityRepository<AccountDelete>>();
                accountDeleteRepository.Setup(m => m.InsertOnCommit(It.IsAny<AccountDelete>()))
                                      .Callback<AccountDelete>(account => DeletedAccounts.Add(account));
                return accountDeleteRepository;
            }

            private Mock<IEntityRepository<User>> SetupUserRepository()
            {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(m => m.CommitChangesAsync())
                              .Returns(Task.CompletedTask);
                userRepository.Setup(m => m.DeleteOnCommit(It.IsAny<User>()))
                              .Callback<User>(user =>
                              {
                                  if (user == _user)
                                  {
                                      _user = null;
                                  }
                                  else
                                  {
                                      DeletedUsers.Add(user);
                                  }
                              });
                return userRepository;
            }

            private Mock<IPackageService> SetupPackageService()
            {
                var packageService = new Mock<IPackageService>();
                if (_user != null)
                {
                    packageService.Setup(m => m.FindPackagesByAnyMatchingOwner(_user, true, It.IsAny<bool>())).Returns(_userPackages);
                }
                //the .Returns(Task.CompletedTask) to avoid NullRef exception by the Mock infrastructure when invoking async operations
                packageService.Setup(m => m.MarkPackageUnlistedAsync(It.IsAny<Package>(), true))
                              .Returns(Task.CompletedTask)
                              .Callback<Package, bool>((package, commit) => { package.Listed = false; });
                return packageService;
            }

            private Mock<ISupportRequestService> SetupSupportRequestService()
            {
                var supportService = new Mock<ISupportRequestService>();
                supportService.Setup(m => m.GetIssues(null, null, null, null)).Returns(SupportRequests);
                if (_user != null)
                {
                    var issue = SupportRequests.Where(i => string.Equals(i.CreatedBy, _user.Username)).FirstOrDefault();
                    supportService.Setup(m => m.DeleteSupportRequestsAsync(_user.Username))
                                  .Returns(Task.FromResult(true))
                                  .Callback(() => SupportRequests.Remove(issue));
                }

                return supportService;
            }

            private Mock<IPackageOwnershipManagementService> SetupPackageOwnershipManagementService()
            {
                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                if (_user != null)
                {
                    packageOwnershipManagementService.Setup(m => m.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>(), false))
                                                     .Returns(Task.CompletedTask)
                                                     .Callback(() =>
                                                     {
                                                         _userPackagesRegistration.Owners.Remove(_user);
                                                         _userPackagesRegistration.ReservedNamespaces.Remove(_reserverdNamespace);
                                                     });

                    packageOwnershipManagementService.Setup(m => m.GetPackageOwnershipRequests(null, null, _user))
                        .Returns(PackageOwnerRequests);

                    packageOwnershipManagementService.Setup(m => m.DeletePackageOwnershipRequestAsync(It.IsAny<PackageRegistration>(), _user))
                        .Returns(Task.CompletedTask)
                        .Callback<PackageRegistration, User>((package, user) =>
                        {
                            PackageOwnerRequests.Remove(PackageOwnerRequests.First(r => r.PackageRegistration == package && r.NewOwner == user));
                        });
                }

                return packageOwnershipManagementService;
            }
        }
    }
}