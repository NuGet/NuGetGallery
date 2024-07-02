// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Features;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery.Services
{
    public class DeleteAccountServiceFacts
    {
        public class TheDeleteGalleryUserAccountAsyncMethod
        {
            private int Key = -1;

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task NullUser(bool isPackageOrphaned)
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestUser(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteAccountAsync(
                    null, 
                    new User("AdminUser") { Key = Key++ },
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task NullAdmin(bool isPackageOrphaned)
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestUser(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteAccountAsync(
                    new User("TestUser") { Key = Key++ },
                    null,
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans));
            }

            /// <summary>
            /// The action to delete a deleted user will be a no-op.
            /// </summary>
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DeleteDeletedUser(bool isPackageOrphaned)
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestUser(ref registration);
                testUser.IsDeleted = true;
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Act
                var result = await deleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: testUser,
                    userToExecuteTheDelete: testUser,
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);
                string expected = $"The account '{testUser.Username}' was already deleted. No action was performed.";
                Assert.Equal(expected, result.Description);
            }

            public static IEnumerable<object[]> DeleteAccount_Data
            {
                get
                {
                    foreach (var isPackageOrphaned in new[] { false, true })
                    {
                        foreach (var orphanPolicy in
                            Enum.GetValues(typeof(AccountDeletionOrphanPackagePolicy)).Cast<AccountDeletionOrphanPackagePolicy>())
                        {
                            yield return new object[] { isPackageOrphaned, orphanPolicy };
                        }
                    }
                }
            }

            /// <summary>
            /// One user with one package that has one namespace reserved and one security policy.
            /// After the account deletion:
            /// The user data (for example the email address) will be cleaned
            /// The package will be unlisted.
            /// The user will have the policies removed.
            /// The namespace will be unassigned from the user.
            /// The information about the deletion will be saved.
            /// </summary>
            [Theory]
            [MemberData(nameof(DeleteAccount_Data))]
            public async Task DeleteHappyUser(bool isPackageOrphaned, AccountDeletionOrphanPackagePolicy orphanPolicy)
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestUser(ref registration);
                var testUserOrganizations = testUser.Organizations.ToList();
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Act
                await deleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: testUser,
                    userToExecuteTheDelete: testUser,
                    orphanPackagePolicy: orphanPolicy);


                if (orphanPolicy == AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans && isPackageOrphaned)
                {
                    Assert.True(registration.Owners.Any(o => o.MatchesUser(testUser)));
                    Assert.NotEmpty(testUser.SecurityPolicies);
                    Assert.True(registration.Packages.Single().Listed);
                    Assert.NotNull(testUser.EmailAddress);
                    Assert.NotNull(testableService.PackagePushedByUser.User);
                    Assert.NotNull(testableService.DeprecationDeprecatedByUser.DeprecatedByUser);
                    Assert.Empty(testableService.DeletedAccounts);
                    Assert.NotEmpty(testableService.PackageOwnerRequests);
                    Assert.False(testableService.HasDeletedOwnerScope);
                    Assert.Empty(testableService.AuditService.Records); 
                    Assert.NotNull(testUser.OrganizationMigrationRequest);
                    Assert.NotEmpty(testUser.OrganizationMigrationRequests);
                    Assert.NotEmpty(testUser.OrganizationRequests);
                    Assert.NotEmpty(testUser.Organizations);
                    Assert.NotNull(testableService.PackageDeletedByUser.DeletedBy);
                    Assert.NotNull(testableService.AccountDeletedByUser.DeletedBy);
                }
                else
                {
                    Assert.False(registration.Owners.Any(o => o.MatchesUser(testUser)));
                    Assert.Empty(testUser.SecurityPolicies);
                    Assert.Equal(
                        orphanPolicy == AccountDeletionOrphanPackagePolicy.UnlistOrphans && isPackageOrphaned,
                        !registration.Packages.Single().Listed);
                    Assert.Null(testUser.EmailAddress);
                    Assert.Null(testableService.PackagePushedByUser.User);
                    Assert.Null(testableService.DeprecationDeprecatedByUser.DeprecatedByUser);
                    Assert.Single(testableService.DeletedAccounts);
                    Assert.Empty(testableService.PackageOwnerRequests);
                    Assert.True(testableService.HasDeletedOwnerScope);
                    Assert.Single(testableService.AuditService.Records);
                    Assert.Null(testUser.OrganizationMigrationRequest);
                    Assert.Empty(testUser.OrganizationMigrationRequests);
                    Assert.Empty(testUser.OrganizationRequests);
                    Assert.Null(testableService.PackageDeletedByUser.DeletedBy);
                    Assert.Null(testableService.AccountDeletedByUser.DeletedBy);

                    Assert.Empty(testUser.Organizations);
                    foreach (var testUserOrganization in testUserOrganizations)
                    {
                        var notDeletedMembers = testUserOrganization.Organization.Members.Where(m => m.Member != testUser);
                        if (notDeletedMembers.Any())
                        {
                            // If an organization that the deleted user was a part of had other members, it should have at least one admin.
                            Assert.Contains(notDeletedMembers, m => m.IsAdmin);
                        }
                        else
                        {
                            // If an organization that the deleted user was a part of had no other members, it should have been deleted.
                            Assert.Contains(testUserOrganization.Organization, testableService.DeletedUsers);
                        }
                    }

                    var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                    Assert.True(deleteRecord != null);
                }

                // Reserved namespaces and support requests are deleted before the request fails due to orphaned packages.
                // Because we are not committing as a transaction in these tests, they remain deleted.
                // In production, they would not be deleted because the transaction they were deleted in would fail.
                Assert.Single(testableService.SupportRequests);
                Assert.Empty(testUser.ReservedNamespaces);

                Assert.NotNull(testableService.PackageDeletedByDifferentUser.DeletedBy);
                Assert.NotNull(testableService.AccountDeletedByDifferentUser.DeletedBy);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenUserIsNotConfirmedTheUserRecordIsDeleted(bool isPackageOrphaned)
            {
                //Arrange
                User testUser = new User("TestsUser") { Key = Key++, UnconfirmedEmailAddress = "user@test.com" };
                var testableService = new DeleteAccountTestService(testUser);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                //Act
                var status = await deleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: testUser,
                    userToExecuteTheDelete: testUser,
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                //Assert
                Assert.True(status.Success);
                Assert.Null(testableService.User);
                Assert.Single(testableService.AuditService.Records);
                Assert.Equal(1, testableService.AuditService.Records.Count);
                var deleteAccountAuditRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.NotNull(deleteAccountAuditRecord);
                Assert.Equal(testUser.Username, deleteAccountAuditRecord.AdminUsername);
                Assert.Equal(testUser.Username, deleteAccountAuditRecord.Username);
                Assert.Equal(DeleteAccountAuditRecord.ActionStatus.Success, deleteAccountAuditRecord.Status);
            }

            [Theory]
            [MemberData(nameof(DeleteAccount_Data))]
            public async Task DeleteOrganization(bool isPackageOrphaned, AccountDeletionOrphanPackagePolicy orphanPolicy)
            {
                // Arrange
                var member = new User("testUser") { Key = Key++ };
                var organization = new Organization("testOrganization")
                {
                    Key = Key++,
                    EmailAddress = "org@test.com"
                };

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
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Act
                var status = await deleteAccountService.DeleteAccountAsync(
                    organization,
                    member,
                    orphanPackagePolicy: orphanPolicy);

                // Assert
                if (orphanPolicy == AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans && isPackageOrphaned)
                {
                    Assert.False(status.Success);
                    Assert.Equal(organization.Confirmed, organization.EmailAddress != null);
                    Assert.True(registration.Owners.Any(o => o.MatchesUser(organization)));
                    Assert.NotEmpty(organization.SecurityPolicies);
                    Assert.NotNull(testableService.PackagePushedByUser.User);
                    Assert.NotNull(testableService.DeprecationDeprecatedByUser.DeprecatedByUser);
                    Assert.Empty(testableService.DeletedAccounts);
                    Assert.NotEmpty(testableService.PackageOwnerRequests);
                    Assert.Empty(testableService.AuditService.Records);
                    Assert.False(testableService.HasDeletedOwnerScope);
                    Assert.Empty(testableService.AuditService.Records);
                    Assert.NotNull(testableService.PackageDeletedByUser.DeletedBy);
                    Assert.NotNull(testableService.AccountDeletedByUser.DeletedBy);
                }
                else
                {
                    Assert.True(status.Success);
                    Assert.Null(organization.EmailAddress);
                    Assert.Equal(
                        orphanPolicy == AccountDeletionOrphanPackagePolicy.UnlistOrphans && isPackageOrphaned,
                        !registration.Packages.Single().Listed);
                    Assert.False(registration.Owners.Any(o => o.MatchesUser(organization)));
                    Assert.Empty(organization.SecurityPolicies);
                    Assert.Null(testableService.PackagePushedByUser.User);
                    Assert.Null(testableService.DeprecationDeprecatedByUser.DeprecatedByUser);
                    Assert.Single(testableService.DeletedAccounts);
                    Assert.Empty(testableService.PackageOwnerRequests);
                    Assert.Single(testableService.AuditService.Records);
                    Assert.True(testableService.HasDeletedOwnerScope);
                    Assert.Null(testableService.PackageDeletedByUser.DeletedBy);
                    Assert.Null(testableService.AccountDeletedByUser.DeletedBy);

                    var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                    Assert.True(deleteRecord != null);
                }

                // Reserved namespaces and support requests are deleted before the request fails due to orphaned packages.
                // Because we are not committing as a transaction in these tests, they remain deleted.
                // In production, they would not be deleted because the transaction they were deleted in would fail.
                Assert.Empty(organization.ReservedNamespaces);
                Assert.Single(testableService.SupportRequests);

                Assert.NotNull(testableService.PackageDeletedByDifferentUser.DeletedBy);
                Assert.NotNull(testableService.AccountDeletedByDifferentUser.DeletedBy);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DeleteUnconfirmedOrganization(bool isPackageOrphaned)
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
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned);

                // Act
                var status = await deleteAccountService.DeleteAccountAsync(
                    organization,
                    member,
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.KeepOrphans);

                // Assert
                Assert.True(status.Success);
                Assert.Null(testableService.User);
                Assert.Empty(registration.Owners);
                Assert.Empty(organization.SecurityPolicies);
                Assert.Empty(organization.ReservedNamespaces);
                Assert.Null(testableService.PackagePushedByUser.User);
                Assert.Null(testableService.DeprecationDeprecatedByUser.DeprecatedByUser);
                Assert.Empty(testableService.DeletedAccounts);
                Assert.Single(testableService.SupportRequests);
                Assert.Empty(testableService.PackageOwnerRequests);
                Assert.True(testableService.HasDeletedOwnerScope);
                Assert.Single(testableService.AuditService.Records);
                
                var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
            }

            [Fact]
            public async Task FailsWhenFeatureFlagsRemovalFails()
            {
                // Arrange
                var testUser = new User("TestsUser") { Key = Key++ };
                var testableService = new DeleteAccountTestService(testUser);
                var deleteAccountService = testableService.GetDeleteAccountService(
                    isPackageOrphaned: false,
                    isFeatureFlagsRemovalSuccessful: false);

                // Act
                var result = await deleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: testUser,
                    userToExecuteTheDelete: testUser);

                // Assert
                Assert.False(result.Success);
                Assert.Equal("TestsUser", result.AccountName);
                Assert.Contains("An exception was encountered while trying to delete the account 'TestsUser'", result.Description);
            }

            /// <summary>
            /// An user that does not own any package but owns a registration will have the registration cleaned after deletion.
            /// </summary>
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task DeleteUserThatOwnsOrphanRegistrationWillCleanTheRegistration(bool multipleOwners)
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestUserWithRegistration(ref registration);
                var newOwner = new User("newOwner");
                newOwner.EmailAddress = "newOwner@email.com";
                if (multipleOwners)
                {                  
                    registration.Owners.Add(newOwner);
                }

                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService(isPackageOrphaned: true);

                // Act
                await deleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: testUser,
                    userToExecuteTheDelete: testUser,
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                // Assert
                if (multipleOwners)
                {
                    Assert.Contains<User>(newOwner, registration.Owners);
                    Assert.Equal(1, registration.Owners.Count());
                }
                else
                {
                    Assert.Empty(registration.Owners);
                }
            }


            private User CreateTestUserWithRegistration(ref PackageRegistration registration)
            {
                var testUser = new User("TestUser") { Key = Key++ };
                testUser.EmailAddress = "user@test.com";
                registration = new PackageRegistration();
                registration.Id = "TestRegistration";
                registration.Owners.Add(testUser);
                return testUser;
            }

            private User CreateTestUser(ref PackageRegistration registration)
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

            private void AddOrganizationMigrationRequest(User testUser)
            {
                var testOrganizationAdmin = new User("TestOrganizationAdmin") { Key = Key++ };

                var request = new OrganizationMigrationRequest { AdminUser = testOrganizationAdmin, NewOrganization = testUser };
                testUser.OrganizationMigrationRequest = request;
                testOrganizationAdmin.OrganizationMigrationRequests.Add(request);
            }

            private void AddOrganizationMigrationRequests(User testUser)
            {
                var testOrganization = new Organization("testOrganization") { Key = Key++ };

                var request = new OrganizationMigrationRequest { AdminUser = testUser, NewOrganization = testOrganization };
                testOrganization.OrganizationMigrationRequest = request;
                testUser.OrganizationMigrationRequests.Add(request);
            }

            private void AddOrganizationRequests(User testUser, bool isAdmin)
            {
                var testOrganization = new Organization("testOrganization") { Key = Key++ };

                var request = new MembershipRequest { IsAdmin = isAdmin, NewMember = testUser, Organization = testOrganization };
                testOrganization.MemberRequests.Add(request);
                testUser.OrganizationRequests.Add(request);
            }

            private void AddOrganization(User testUser, bool isAdmin, bool hasAdminMember, bool hasCollaboratorMember)
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
            private static ReservedNamespace _reservedNamespace = new ReservedNamespace("Ns1", false, false);
            private Credential _credential = new Credential("CredType", "CredValue");
            private UserSecurityPolicy _securityPolicy = new UserSecurityPolicy("PolicyName", SubscriptionName);
            private PackageRegistration _userPackagesRegistration = null;
            private ICollection<Package> _userPackages;
            private bool _hasDeletedCredentialWithOwnerScope = false;

            public List<AccountDelete> DeletedAccounts = new List<AccountDelete>();
            public List<User> DeletedUsers = new List<User>();
            public List<Issue> SupportRequests = new List<Issue>();
            public Package PackagePushedByUser;
            public PackageDeprecation DeprecationDeprecatedByUser;
            public List<PackageOwnerRequest> PackageOwnerRequests = new List<PackageOwnerRequest>();
            public FakeAuditingService AuditService = new FakeAuditingService();
            public bool HasDeletedOwnerScope => _hasDeletedCredentialWithOwnerScope;

            public AccountDelete AccountDeletedByUser { get; }
            public AccountDelete AccountDeletedByDifferentUser { get; }
            public PackageDelete PackageDeletedByUser { get; }
            public PackageDelete PackageDeletedByDifferentUser { get; }


            public DeleteAccountTestService(User user)
            {
                _user = user;
                _userPackages = new List<Package>();

                AuditService = new FakeAuditingService();
            }

            public DeleteAccountTestService(User user, PackageRegistration userPackagesRegistration)
            {
                _user = user;
                _user.ReservedNamespaces.Add(_reservedNamespace);
                _user.Credentials.Add(_credential);
                _credential.User = _user;
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

                AccountDeletedByUser = new AccountDelete { DeletedBy = _user, DeletedByKey = _user.Key };
                AccountDeletedByDifferentUser = new AccountDelete { DeletedBy = new User { Key = 1111 }, DeletedByKey = 1111 };
                PackageDeletedByUser = new PackageDelete { DeletedBy = _user, DeletedByKey = _user.Key };
                PackageDeletedByDifferentUser = new PackageDelete { DeletedBy = new User { Key = 1111 }, DeletedByKey = 1111 };

                PackagePushedByUser = new Package
                {
                    User = _user,
                    UserKey = _user.Key
                };

                DeprecationDeprecatedByUser = new PackageDeprecation
                {
                    DeprecatedByUser = _user,
                    DeprecatedByUserKey = _user.Key
                };
            }

            public DeleteAccountTestService()
            {
            }

            public DeleteAccountService GetDeleteAccountService(bool isPackageOrphaned, bool isFeatureFlagsRemovalSuccessful = true)
            {
                return new DeleteAccountService(
                    SetupAccountDeleteRepository().Object,
                    SetupPackageDeleteRepository().Object,
                    SetupDeprecationRepository().Object,
                    SetupUserRepository().Object,
                    SetupScopeRepository().Object,
                    SetupEntitiesContext().Object,
                    SetupPackageService(isPackageOrphaned).Object,
                    SetupPackageUpdateService().Object,
                    SetupPackageOwnershipManagementService().Object,
                    SetupReservedNamespaceService().Object,
                    SetupSecurityPolicyService().Object,
                    SetupAuthenticationService().Object,
                    SetupSupportRequestService().Object,
                    SetupFeatureFlagStorageService(isFeatureFlagsRemovalSuccessful).Object,
                    AuditService,
                    SetupTelemetryService().Object);
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

            private Mock<IEntitiesContext> SetupEntitiesContext()
            {
                var mockContext = new Mock<IEntitiesContext>();
                var database = new Mock<IDatabase>();
                database
                    .Setup(x => x.BeginTransaction())
                    .Returns(() => new Mock<IDbContextTransaction>().Object);

                mockContext
                    .Setup(m => m.GetDatabase())
                    .Returns(database.Object);

                var packageDbSet = FakeEntitiesContext.CreateDbSet<Package>();
                mockContext
                    .Setup(x => x.Packages)
                    .Returns(packageDbSet);

                if (PackagePushedByUser != null)
                {
                    packageDbSet.Add(PackagePushedByUser);
                }

                return mockContext;
            }

            private Mock<IReservedNamespaceService> SetupReservedNamespaceService()
            {
                var namespaceService = new Mock<IReservedNamespaceService>();
                if (_user != null)
                {
                    namespaceService.Setup(m => m.DeleteOwnerFromReservedNamespaceAsync(It.IsAny<string>(), It.IsAny<string>(), false))
                        .Returns(Task.CompletedTask)
                        .Callback(() => _user.ReservedNamespaces.Remove(_reservedNamespace));
                }

                return namespaceService;
            }

            private Mock<ISecurityPolicyService> SetupSecurityPolicyService()
            {
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                if (_user != null)
                {
                    securityPolicyService.Setup(m => m.UnsubscribeAsync(_user, SubscriptionName, false))
                        .Returns(Task.CompletedTask)
                        .Callback(() => _user.SecurityPolicies.Remove(_securityPolicy));
                }

                return securityPolicyService;
            }

            private Mock<IEntityRepository<AccountDelete>> SetupAccountDeleteRepository()
            {
                var accountDeleteRepository = new Mock<IEntityRepository<AccountDelete>>();

                if (AccountDeletedByUser != null)
                {
                    accountDeleteRepository
                        .Setup(m => m.GetAll())
                        .Returns(new[] { AccountDeletedByUser, AccountDeletedByDifferentUser }.AsQueryable());
                }

                accountDeleteRepository
                    .Setup(m => m.InsertOnCommit(It.IsAny<AccountDelete>()))
                    .Callback<AccountDelete>(account => DeletedAccounts.Add(account));

                return accountDeleteRepository;
            }

            private Mock<IEntityRepository<PackageDelete>> SetupPackageDeleteRepository()
            {
                var packageDeleteRepository = new Mock<IEntityRepository<PackageDelete>>();

                if (PackageDeletedByUser != null)
                {
                    packageDeleteRepository
                        .Setup(m => m.GetAll())
                        .Returns(new[] { PackageDeletedByUser, PackageDeletedByDifferentUser }.AsQueryable());
                }

                return packageDeleteRepository;
            }
          
            private Mock<IEntityRepository<PackageDeprecation>> SetupDeprecationRepository()
            {
                var deprecationRepository = new Mock<IEntityRepository<PackageDeprecation>>();
                var deprecations = DeprecationDeprecatedByUser == null
                    ? Enumerable.Empty<PackageDeprecation>()
                    : new[] { DeprecationDeprecatedByUser };

                deprecationRepository
                    .Setup(x => x.GetAll())
                    .Returns(deprecations.AsQueryable());

                return deprecationRepository;
            }

            private Mock<IEntityRepository<User>> SetupUserRepository()
            {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository
                    .Setup(m => m.DeleteOnCommit(It.IsAny<User>()))
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

            private Mock<IEntityRepository<Scope>> SetupScopeRepository()
            {
                var scopeRepository = new Mock<IEntityRepository<Scope>>();
                
                var user = new User("userWithApiKeyScopedToDeletedUser") { Key = 54325 };
                var credential = new Credential("CredentialType", "CredentialValue") { User = user };
                user.Credentials.Add(credential);

                var scope = new Scope(_user, "subject1", "action1") { OwnerKey = _user.Key };
                credential.Scopes = new[] { scope };
                scope.Credential = credential;

                scopeRepository
                    .Setup(m => m.GetAll())
                    .Returns(new[] { scope }.AsQueryable());
                scopeRepository
                    .Setup(m => m.DeleteOnCommit(It.IsAny<Scope>()))
                    .Throws(new Exception("Scopes should be deleted by the AuthenticationService!"));

                return scopeRepository;
            }

            private Mock<IPackageService> SetupPackageService(bool isPackageOrphaned)
            {
                var packageService = new Mock<IPackageService>();
                if (_user != null)
                {
                    packageService.Setup(m => m.FindPackagesByAnyMatchingOwner(
                        _user, true, It.IsAny<bool>())).Returns(_userPackages);
                    var packageRegistraionList = new List<PackageRegistration>();
                    if(_userPackagesRegistration != null)
                    {
                        packageRegistraionList.Add(_userPackagesRegistration);
                    }
                    packageService.Setup(m => m.FindPackageRegistrationsByOwner(_user)).Returns(packageRegistraionList.AsQueryable());
                }

                packageService
                    .Setup(p => p.WillPackageBeOrphanedIfOwnerRemoved(It.IsAny<PackageRegistration>(), It.IsAny<User>()))
                    .Returns(isPackageOrphaned);

                return packageService;
            }

            private Mock<IPackageUpdateService> SetupPackageUpdateService()
            {
                var packageUpdateService = new Mock<IPackageUpdateService>();

                //the .Returns(Task.CompletedTask) to avoid NullRef exception by the Mock infrastructure when invoking async operations
                packageUpdateService
                    .Setup(m => m.MarkPackageUnlistedAsync(It.IsAny<Package>(), false, false))
                    .Returns(Task.CompletedTask)
                    .Callback<Package, bool, bool>(
                        (package, commitChanges, updateIndex) => { package.Listed = false; });

                return packageUpdateService;
            }

            private Mock<AuthenticationService> SetupAuthenticationService()
            {
                var authService = new Mock<AuthenticationService>();
                authService
                    .Setup(m => m.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), false))
                    .Callback<User, Credential, bool>((user, credential, commitChanges) =>
                    {
                        user.Credentials.Remove(credential);
                        if (credential.Scopes.Any(s => s.Owner == _user))
                        {
                            _hasDeletedCredentialWithOwnerScope = true;
                        }
                    })
                    .Returns(Task.CompletedTask);

                return authService;
            }

            private Mock<ISupportRequestService> SetupSupportRequestService()
            {
                var supportService = new Mock<ISupportRequestService>();
                supportService.Setup(m => m.GetIssues(null, null, null, null)).Returns(SupportRequests);
                if (_user != null)
                {
                    var issue = SupportRequests.FirstOrDefault(i => string.Equals(i.CreatedBy, _user.Username));
                    supportService.Setup(m => m.DeleteSupportRequestsAsync(_user))
                        .Returns(Task.FromResult(true))
                        .Callback(() => SupportRequests.Remove(issue));
                }

                return supportService;
            }

            private Mock<IEditableFeatureFlagStorageService> SetupFeatureFlagStorageService(bool succeeds)
            {
                var flagsService = new Mock<IEditableFeatureFlagStorageService>();

                if (!succeeds)
                {
                    flagsService
                        .Setup(f => f.RemoveUserAsync(It.IsAny<User>()))
                        .ThrowsAsync(new InvalidOperationException("Failed to remove user"));
                }

                return flagsService;
            }

            private Mock<IPackageOwnershipManagementService> SetupPackageOwnershipManagementService()
            {
                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                if (_user != null)
                {
                    packageOwnershipManagementService
                        .Setup(m => m.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>(), false))
                        .Returns(Task.CompletedTask)
                        .Callback(() =>
                        {
                            _userPackagesRegistration.Owners.Remove(_user);
                            _userPackagesRegistration.ReservedNamespaces.Remove(_reservedNamespace);
                        });

                    packageOwnershipManagementService.Setup(m => m.GetPackageOwnershipRequests(null, null, _user))
                        .Returns(PackageOwnerRequests);

                    packageOwnershipManagementService.Setup(m => m.DeletePackageOwnershipRequestAsync(It.IsAny<PackageRegistration>(), _user, false))
                        .Returns(Task.CompletedTask)
                        .Callback<PackageRegistration, User, bool>((package, user, commitChanges) =>
                        {
                            PackageOwnerRequests.Remove(PackageOwnerRequests.First(r => r.PackageRegistration == package && r.NewOwner == user));
                        });
                }

                return packageOwnershipManagementService;
            }
            private Mock<ITelemetryService> SetupTelemetryService()
            {
                return new Mock<ITelemetryService>();
            }
        }
    }
}