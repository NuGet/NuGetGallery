// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Security;
using Xunit;
using Moq;

namespace NuGetGallery.Services
{
    public class DeleteAccountServiceFacts
    {
        public class TheDeleteGalleryUserAccountAsyncMethod
        {
            [Fact]
            public async Task WhenAccountIsOrganization_DoesNotDelete()
            {
                // Arrange
                var fakes = new Fakes();
                var testableService = new DeleteAccountTestService(fakes.Organization, fakes.Package);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var result = await deleteAccountService.DeleteGalleryUserAccountAsync(
                    fakes.Organization,
                    fakes.Admin,
                    "signature",
                    unlistOrphanPackages: true,
                    commitAsTransaction: false);

                // Assert
                Assert.False(result.Success);

                var expected = string.Format(CultureInfo.CurrentCulture,
                    Strings.AccountDelete_OrganizationDeleteNotImplemented,
                    fakes.Organization.Username);
                Assert.Equal(expected, result.Description);
            }

            [Fact]
            public async Task NullUser()
            {
                // Arrange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(null, new User("AdminUser"), "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
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
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(new User("TestUser"), null, "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
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
                    DeleteGalleryUserAccountAsync(userToBeDeleted: testUser,
                                                admin: testUser,
                                                signature: signature,
                                                unlistOrphanPackages: true,
                                                commitAsTransaction: false);
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
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var signature = "Hello";
                await deleteAccountService.
                    DeleteGalleryUserAccountAsync(userToBeDeleted: testUser,
                                                admin: testUser,
                                                signature: signature,
                                                unlistOrphanPackages: true,
                                                commitAsTransaction: false);

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
                var deleteRecord = testableService.AuditService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
            }

            private static User CreateTestData(ref PackageRegistration registration)
            {
                var testUser = new User("TestUser");
                testUser.EmailAddress = "user@test.com";

                AddOrganizationMigrationRequest(testUser);
                AddOrganizationMigrationRequests(testUser);
                AddOrganizationRequests(testUser, false);
                AddOrganizationRequests(testUser, true);

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
                var testOrganizationAdmin = new User("TestOrganizationAdmin");

                var request = new OrganizationMigrationRequest { AdminUser = testOrganizationAdmin, NewOrganization = testUser };
                testUser.OrganizationMigrationRequest = request;
                testOrganizationAdmin.OrganizationMigrationRequests.Add(request);
            }

            private static void AddOrganizationMigrationRequests(User testUser)
            {
                var testOrganization = new Organization("testOrganization");

                var request = new OrganizationMigrationRequest { AdminUser = testUser, NewOrganization = testOrganization };
                testOrganization.OrganizationMigrationRequest = request;
                testUser.OrganizationMigrationRequests.Add(request);
            }

            private static void AddOrganizationRequests(User testUser, bool isAdmin)
            {
                var testOrganization = new Organization("testOrganization");

                var request = new MembershipRequest { IsAdmin = isAdmin, NewMember = testUser, Organization = testOrganization };
                testOrganization.MemberRequests.Add(request);
                testUser.OrganizationRequests.Add(request);
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
            public List<Issue> SupportRequests = new List<Issue>();
            public List<PackageOwnerRequest> PackageOwnerRequests = new List<PackageOwnerRequest>();
            public FakeAuditingService AuditService = new FakeAuditingService();

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
                    securityPolicyService.Setup(m => m.UnsubscribeAsync(It.IsAny<User>(), SubscriptionName))
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