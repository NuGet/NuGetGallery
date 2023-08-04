// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            [InlineData("")]
            [InlineData(null)]
            public void WhenInvalidNewUsernameReturnsBadRequestStatusCode(string newUsername)
            {
                var result = ChangeUsernameController.ValidateNewUsername(newUsername) as JsonResult;

                Assert.Equal(((int)HttpStatusCode.BadRequest), ChangeUsernameController.Response.StatusCode);
                Assert.Equal("Username cannot be null or empty.", result.Data);
            }

            [Theory]
            [InlineData("_aaa_", false, true)]
            [InlineData("testUser", true, false)]
            [InlineData("availableUsername", true, true)]
            public void WhenValidNewUsernameReturnsValidations(string newUsername, bool isFormatValid, bool isAvailable)
            {
                var result = ChangeUsernameController.ValidateNewUsername(newUsername) as JsonResult;

                var validations = result.Data as ValidateUsernameResult;

                Assert.Equal(isFormatValid, validations.IsFormatValid);
                Assert.Equal(isAvailable, validations.IsAvailable);
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

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(IndividualAccount.Username, It.IsAny<bool>()))
                    .Returns(IndividualAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(IndividualAccount.EmailAddress, false))
                    .Returns(IndividualAccount);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(OrganizationAccount.Username, true))
                    .Returns(OrganizationAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(OrganizationAccount.EmailAddress, true))
                    .Returns(OrganizationAccount);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(AvailableUsername, true))
                    .ReturnsNull();

                ChangeUsernameController = new ChangeUsernameController(
                    GetMock<IUserService>().Object,
                    GetMock<IEntityRepository<User>>().Object,
                    GetMock<IEntitiesContext>().Object,
                    GetMock<IDateTimeProvider>().Object,
                    GetMock<IAuditingService>().Object);

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
