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
            public async Task WhenAccountIsOrganizationMember_DoesNotDelete()
            {
                // Arrange
                var fakes = new Fakes();
                var account = fakes.OrganizationCollaborator;
                var testableService = new DeleteAccountTestService(account, fakes.Package);
                var deleteAccountService = testableService.GetDeleteAccountService();

                // Act
                var result = await deleteAccountService.DeleteGalleryUserAccountAsync(
                    account,
                    fakes.Admin,
                    "signature",
                    unlistOrphanPackages: true,
                    commitAsTransaction: false);

                // Assert
                Assert.False(result.Success);

                var expected = string.Format(CultureInfo.CurrentCulture,
                    Strings.AccountDelete_OrganizationMemberDeleteNotImplemented,
                    account.Username);
                Assert.Equal(expected, result.Description);
            }

            [Fact]
            public async Task NullUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(null, new User("AdminUser"), "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
            }

            [Fact]
            public async Task NullAdmin()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(new User("TestUser"),null , "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
            }

            /// <summary>
            /// The action to delete a deleted user will be noop.
            /// </summary>
            /// <returns></returns>
            [Fact]
            public async Task DeleteDeletedUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                testUser.IsDeleted = true;
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Act
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
            /// <returns></returns>
            [Fact]
            public async Task DeleteHappyUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Act
                var signature = "Hello";
                await deleteAccountService.
                    DeleteGalleryUserAccountAsync(userToBeDeleted: testUser,
                                                admin: testUser,
                                                signature: signature,
                                                unlistOrphanPackages: true,
                                                commitAsTransaction: false);

                Assert.Equal<int>(0, registration.Owners.Count());
                Assert.Equal<int>(0, testUser.SecurityPolicies.Count());
                Assert.Equal<int>(0, testUser.ReservedNamespaces.Count());
                Assert.Equal<bool>(false, registration.Packages.ElementAt(0).Listed);
                Assert.Null(testUser.EmailAddress);
                Assert.Equal<int>(1, testableService.DeletedAccounts.Count());
                Assert.Equal<string>(signature, testableService.DeletedAccounts.ElementAt(0).Signature);
                Assert.Equal<int>(1, testableService.SupportRequests.Count);
            }

            private static User CreateTestData(ref PackageRegistration registration)
            {
                User testUser = new User();
                testUser.Username = "TestsUser";
                testUser.EmailAddress = "user@test.com";

                registration = new PackageRegistration();
                registration.Owners.Add(testUser);

                Package p = new Package()
                {
                    Description = "TestPackage",
                    Key = 1
                };
                p.PackageRegistration = registration;
                registration.Packages.Add(p);
                return testUser;
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
                    HistoryEntries = new List<History>() { new History() { EditedBy = user.Username, IssueId = 1, Key = 1, IssueStatusId = IssueStatusKeys.New} }
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
                    SetupSupportRequestService().Object);
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
                namespaceService.Setup(m => m.DeleteOwnerFromReservedNamespaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                                .Returns(Task.CompletedTask)
                                .Callback(() => _user.ReservedNamespaces.Remove(_reserverdNamespace));
                return namespaceService;
            }

            private Mock<ISecurityPolicyService> SetupSecurityPolicyService()
            {
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService.Setup(m => m.UnsubscribeAsync(It.IsAny<User>(), SubscriptionName))
                                     .Returns(Task.CompletedTask)
                                     .Callback(() => _user.SecurityPolicies.Remove(_securityPolicy));
                return securityPolicyService;
            }

            private Mock<IEntityRepository<AccountDelete>> SetupAccountDeleteRepository()
            {
                var acountDeleteRepository = new Mock<IEntityRepository<AccountDelete>>();
                acountDeleteRepository.Setup(m => m.InsertOnCommit(It.IsAny<AccountDelete>()))
                                      .Callback<AccountDelete>(account => DeletedAccounts.Add(account));
                return acountDeleteRepository;
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
                packageService.Setup(m => m.FindPackagesByAnyMatchingOwner(_user, true, It.IsAny<bool>())).Returns(_userPackages);
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
                var issue = SupportRequests.Where(i => string.Equals(i.CreatedBy, _user.Username)).FirstOrDefault();
                supportService.Setup(m => m.DeleteSupportRequestsAsync(_user.Username))
                              .Returns(Task.FromResult<bool>(true))
                              .Callback( () => SupportRequests.Remove(issue));

                return supportService;
            }

            private Mock<IPackageOwnershipManagementService> SetupPackageOwnershipManagementService()
            {
                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                packageOwnershipManagementService.Setup(m => m.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>(), false))
                                                 .Returns(Task.CompletedTask)
                                                 .Callback(() => 
                                                 {
                                                     _userPackagesRegistration.Owners.Remove(_user);
                                                     _userPackagesRegistration.ReservedNamespaces.Remove(_reserverdNamespace);
                                                 }
                                                            );
                return packageOwnershipManagementService;
            }
        }
    }
}
