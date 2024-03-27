// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ChangeUsernameControllerFacts
    {
        public class VerifyAccountMethod : FactsBase
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenInvalidParameterReturnsBadRequestStatusCode(string accountEmailOrUsername)
            {
                var result = ChangeUsernameController.VerifyAccount(accountEmailOrUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Email or username cannot be null or empty.", result.Data);
            }

            [Theory]
            [InlineData("test@example.com")]
            [InlineData("test")]
            public void WhenAccountNotFoundReturnsNotFoundStatusCode(string accountEmailOrUsername)
            {
                var result = ChangeUsernameController.VerifyAccount(accountEmailOrUsername) as JsonResult;

                var userService = GetMock<IUserService>();

                GetMock<IUserService>().Verify(u => u.FindByUsername(It.IsAny<string>(), false));
                GetMock<IUserService>().Verify(u => u.FindByEmailAddress(It.IsAny<string>()));

                Assert.Equal(((int)HttpStatusCode.NotFound), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Account was not found.", result.Data);
            }

            [Fact]
            public void WhenValidUsernameAccountReturnsAccount()
            {
                var result = ChangeUsernameController.VerifyAccount(IndividualAccount.Username) as JsonResult;
                var account = result.Data as ValidateAccountResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(IndividualAccount.Username, false));
                GetMock<IUserService>().Verify(u => u.FindByEmailAddress(It.IsAny<string>()), Times.Never());

                Assert.Equal(IndividualAccount.Username, account.Account.Username);
                Assert.Equal(IndividualAccount.EmailAddress, account.Account.EmailAddress);
            }

            [Fact]
            public void WhenValidEmailAddressAccountReturnsAccount()
            {
                var result = ChangeUsernameController.VerifyAccount(IndividualAccount.EmailAddress) as JsonResult;
                var account = result.Data as ValidateAccountResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(IndividualAccount.EmailAddress, false), Times.Once);
                GetMock<IUserService>().Verify(u => u.FindByEmailAddress(IndividualAccount.EmailAddress), Times.Never());

                Assert.Equal(IndividualAccount.Username, account.Account.Username);
                Assert.Equal(IndividualAccount.EmailAddress, account.Account.EmailAddress);
            }

            [Fact]
            public void WhenValidIndividualAccountReturnsAccountWithNoAdministrators()
            {
                var result = ChangeUsernameController.VerifyAccount(IndividualAccount.Username) as JsonResult;
                var account = result.Data as ValidateAccountResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(IndividualAccount.Username, false));

                Assert.Equal(IndividualAccount.Username, account.Account.Username);
                Assert.Equal(IndividualAccount.EmailAddress, account.Account.EmailAddress);
                Assert.Empty(account.Administrators);
            }

            [Fact]
            public void WhenValidOrganizationAccountReturnsAccountWithAdministrators()
            {
                var result = ChangeUsernameController.VerifyAccount(OrganizationAccount.Username) as JsonResult;
                var account = result.Data as ValidateAccountResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(OrganizationAccount.Username, false));

                Assert.Equal(OrganizationAccount.Username, account.Account.Username);
                Assert.Equal(OrganizationAccount.EmailAddress, account.Account.EmailAddress);
                Assert.NotEmpty(account.Administrators);
                Assert.Equal(OrganizationAdministrator.Username, account.Administrators.First().Username);
                Assert.Equal(OrganizationAdministrator.EmailAddress, account.Administrators.First().EmailAddress);
            }
        }

        public class ValidateNewUsernameMethod : FactsBase
        {
            [Theory]
            [InlineData("", true)]
            [InlineData("", false)]
            [InlineData(null, true)]
            [InlineData(null, false)]
            public void WhenInvalidNewUsernameReturnsBadRequestStatusCode(string newUsername, bool checkOwnedPackages)
            {
                var result = ChangeUsernameController.ValidateNewUsername(newUsername, checkOwnedPackages, oldUsername: "testUser") as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Username cannot be null or empty.", result.Data);
            }

            [Theory]
            [InlineData("", true)]
            [InlineData("", false)]
            [InlineData(null, true)]
            [InlineData(null, false)]
            public void WhenInvalidOldUsernameReturnsBadRequestStatusCode(string oldUsername, bool checkOwnedPackages)
            {
                var result = ChangeUsernameController.ValidateNewUsername("testUser", checkOwnedPackages, oldUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Old username cannot be null or empty.", result.Data);
            }

            [Theory]
            [InlineData("_aaa_", false, true)]
            [InlineData("testUser", true, false)]
            [InlineData("availableUsername", true, true)]
            public void WhenValidNewUsernameReturnsValidations(string newUsername, bool isFormatValid, bool isAvailable)
            {
                var result = ChangeUsernameController.ValidateNewUsername(newUsername, checkOwnedPackages: false, oldUsername: "testUser") as JsonResult;

                var validations = result.Data as ValidateUsernameResult;

                Assert.Equal(isFormatValid, validations.IsFormatValid);
                Assert.Equal(isAvailable, validations.IsAvailable);
            }

            [Theory]
            [InlineData("someNewUsername", "testOrganization", false)]
            [InlineData("someNewUsername", "testUser", true)]
            public void WhenCheckOwnedPackagesReturnThem(string newUsername, string oldUsername, bool expectPackages)
            {
                var result = ChangeUsernameController.ValidateNewUsername(newUsername, checkOwnedPackages: true, oldUsername) as JsonResult;

                var validations = result.Data as ValidateUsernameResult;

                Assert.True(validations.IsFormatValid);
                Assert.True(validations.IsAvailable);

                if(expectPackages)
                {
                    Assert.NotEmpty(validations.OwnedPackageIds);
                }
                else
                {
                    Assert.Empty(validations.OwnedPackageIds);
                }
            }
        }

        public class ChangeUsernameMethod : FactsBase
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async void WhenInvalidOldUsernameReturnsBadRequestStatusCode(string oldUsername)
            {
                var result = await ChangeUsernameController.ChangeUsername(oldUsername, AvailableUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Old username cannot be null or empty.", result.Data);
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async void WhenInvalidNewUsernameReturnsBadRequestStatusCode(string newUsername)
            {
                var result = await ChangeUsernameController.ChangeUsername("accountUsername", newUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("New username cannot be null or empty.", result.Data);
            }

            [Fact]
            public async void WhenAccountNotFoundReturnsNotFoundStatusCode()
            {
                var result = await ChangeUsernameController.ChangeUsername("NotFoundAccount", AvailableUsername) as JsonResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(It.IsAny<string>(), false));

                Assert.Equal(((int)HttpStatusCode.NotFound), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Old username account was not found.", result.Data);
            }

            [Theory]
            [InlineData("_aaa_")]
            [InlineData("testOrganization")]
            public async void WhenNewUsernameValidationsFailedReturnsBadRequestStatusCode(string newUsername)
            {
                var result = await ChangeUsernameController.ChangeUsername(IndividualAccount.Username, newUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("New username validation failed.", result.Data);
            }

            [Fact]
            public async void WhenValidAccountSaveAuditRecord()
            {
                UserAuditRecord auditRecord = null;
                GetMock<IAuditingService>()
                    .Setup(a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()))
                    .Returns(Task.CompletedTask)
                    .Callback<AuditRecord>(r => auditRecord = r as UserAuditRecord);

                var result = await ChangeUsernameController.ChangeUsername(IndividualAccount.Username, AvailableUsername) as JsonResult;

                GetMock<IAuditingService>().Verify(a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()));
                Assert.Equal(AvailableUsername, auditRecord.Username);
            }

            [Fact]
            public async void WhenValidAccountAndNewUsernameReturnsOkStatusCode()
            {
                User newAccount = null;
                GetMock<IEntityRepository<User>>()
                    .Setup(r => r.InsertOnCommit(It.IsAny<User>()))
                    .Callback<User>(u => newAccount = u);
                var oldUsername = IndividualAccount.Username;

                var result = await ChangeUsernameController.ChangeUsername(IndividualAccount.Username, AvailableUsername) as JsonResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(oldUsername, false));
                GetMock<IUserService>().Verify(u => u.FindByUsername(AvailableUsername, true));
                GetMock<IEntityRepository<User>>().Verify(r => r.InsertOnCommit(It.IsAny<User>()));
                GetMock<IEntitiesContext>().Verify(c => c.SaveChangesAsync());

                Assert.Equal(oldUsername, newAccount.Username);
                Assert.Equal(AvailableUsername, IndividualAccount.Username);
                Assert.Equal(((int)HttpStatusCode.OK), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Account renamed successfully!", result.Data);
            }

            [Fact]
            public async void WhenValidAccountCasingChangeAndNewUsernameReturnsOkStatusCode()
            {
                var oldUsername = IndividualAccount.Username;

                var result = await ChangeUsernameController.ChangeUsername(IndividualAccount.Username, IndividualAccount.Username.ToUpper()) as JsonResult;

                GetMock<IUserService>().Verify(u => u.FindByUsername(oldUsername, false));
                GetMock<IUserService>().Verify(u => u.FindByUsername(oldUsername.ToUpper(), true));
                GetMock<IEntityRepository<User>>().Verify(r => r.InsertOnCommit(It.IsAny<User>()), Times.Never());
                GetMock<IEntitiesContext>().Verify(c => c.SaveChangesAsync());

                Assert.Equal(oldUsername.ToUpper(), IndividualAccount.Username);
                Assert.Equal(((int)HttpStatusCode.OK), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Account renamed successfully!", result.Data);
            }

            [Fact]
            public async void FailWhenAccountCasingNotAvailable()
            {
                var result = await ChangeUsernameController.ChangeUsername(IndividualAccount.Username, OrganizationAccount.Username.ToUpper()) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("New username validation failed.", result.Data);
            }
        }

        public class FactsBase : TestContainer
        {
            protected ChangeUsernameController ChangeUsernameController;
            protected User IndividualAccount;
            protected Organization OrganizationAccount;
            protected User OrganizationAdministrator;
            protected const string AvailableUsername = "availableUsername";

            public FactsBase()
            {
                IndividualAccount = new Fakes().User;
                OrganizationAccount = new Fakes().Organization;
                OrganizationAdministrator = new Fakes().OrganizationAdmin;

                // FindByUsername is case-insensitive in the database, so we need to simulate that here
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.Is<string>(x => string.Equals(IndividualAccount.Username, x, System.StringComparison.OrdinalIgnoreCase)), It.IsAny<bool>()))
                    .Returns(IndividualAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.Is<string>(x => string.Equals(IndividualAccount.EmailAddress, x, System.StringComparison.OrdinalIgnoreCase)), false))
                    .Returns(IndividualAccount);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.Is<string>(x => string.Equals(OrganizationAccount.Username, x, System.StringComparison.OrdinalIgnoreCase)), true))
                    .Returns(OrganizationAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.Is<string>(x => string.Equals(OrganizationAccount.EmailAddress, x, System.StringComparison.OrdinalIgnoreCase)), true))
                    .Returns(OrganizationAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.Is<string>(x => string.Equals(AvailableUsername, x, System.StringComparison.OrdinalIgnoreCase)), true))
                    .ReturnsNull();

                GetMock<IPackageService>().Setup(p => p.FindPackagesByOwner(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<bool>()))
                    .Returns(new List<Package>());
                GetMock<IPackageService>().Setup(p => p.FindPackagesByOwner(It.Is<User>(u => u.Username == "testUser"), It.IsAny<bool>(), It.IsAny<bool>()))
                    .Returns(new List<Package>() { new Package() { PackageRegistration = new PackageRegistration() { Id = "testPackage" } }});

                ChangeUsernameController = new ChangeUsernameController(
                    GetMock<IUserService>().Object,
                    GetMock<IEntityRepository<User>>().Object,
                    GetMock<IEntitiesContext>().Object,
                    GetMock<IDateTimeProvider>().Object,
                    GetMock<IAuditingService>().Object,
                    GetMock<IPackageService>().Object);

                TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), ChangeUsernameController);
            }

            protected override void Dispose(bool disposing)
            {
                if (ChangeUsernameController != null)
                {
                    ChangeUsernameController.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
