// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class SupportControllerFacts
    {
        public class TheIndexMethod : TestContainer
        {
            [Fact]
            public void DeletedSupportIssuesAreFiltered()
            {
                // Arrange
                var admin = new User()
                {
                    Username = "admin1",
                    EmailAddress = "admin1.coldmail.com",
                    Key = 111,
                    EmailAllowed = true,
                    Roles = new List<Role>()
                    {
                        new Role() { Name = CoreConstants.AdminRoleName }
                    }
                };

                var users = new List<User>(){
                                                admin,
                                                new User(){ Username = "user1", EmailAddress = "email1.coldmail.com", Key = 1, EmailAllowed = true },
                                                new User(){ Username = "", EmailAddress = "", Key = 2, EmailAllowed = false, IsDeleted = true }};
                var issues = new List<Issue>(){
                                                new Issue(){
                                                    UserKey = 1,
                                                    OwnerEmail = "email1.coldmail.com",
                                                    Key = 1, CreatedBy = "user1",
                                                    IssueTitle = "IssueTitle1",
                                                    IssueStatus = new IssueStatus(){ Name = "Resolved", Key = 1 } , IssueStatusId = IssueStatusKeys.Resolved },
                                                new Issue(){
                                                    UserKey = 1,
                                                    OwnerEmail = "email1.coldmail.com",
                                                    Key = 2,
                                                    CreatedBy = "user1",
                                                    IssueTitle = "IssueTitle2",
                                                    IssueStatus = new IssueStatus(){ Name = "New", Key = 2 } , IssueStatusId = IssueStatusKeys.New },
                                                new Issue(){
                                                    UserKey = 2,
                                                    OwnerEmail = "",
                                                    Key = 3,
                                                    CreatedBy = null,
                                                    IssueTitle = "DeleteAccount" }};
                var admins = new List<Admin>() { new Admin() { GalleryUsername = "admin1", Key = 1 } };
                var issuesStatuses = new List<IssueStatus>(){
                                                new IssueStatus() { Name = "Resolved", Key = 1 },
                                                new IssueStatus() { Name = "New", Key = 2 } };

                var supportRequestService = new Mock<ISupportRequestService>();
                supportRequestService.Setup(m => m.GetIssues(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>())).Returns(issues);
                supportRequestService.Setup(m => m.GetAllAdmins()).Returns(admins);
                supportRequestService.Setup(m => m.GetAllIssueStatuses()).Returns(issuesStatuses);
                supportRequestService.Setup(m => m.GetIssueCount(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<int?>())).Returns(issues.Count);

                var userService = new Mock<IUserService>();
                IDictionary<int, string> emailAddresses = new Dictionary<int, string>();
                emailAddresses.Add(111, "admin1.coldmail.com");
                emailAddresses.Add(1, "email1.coldmail.com");
                emailAddresses.Add(2, string.Empty);
                userService.Setup(m => m.GetEmailAddressesForUserKeysAsync(It.IsAny<IReadOnlyCollection<int>>())).Returns(Task.FromResult(emailAddresses));

                var controller = CreateController(supportRequestService.Object, userService.Object, admin);

                // Act
                var viewResult = (ViewResult)controller.Index().Result;
                var model = viewResult.Model as SupportRequestsViewModel;

                // Assert
                Assert.NotNull(model);
                Assert.Equal<int>(2, model.Issues.Count);
                Assert.Equal<int>(2, model.Issues.Count(i => i.CreatedBy != null));
            }
        }

        private static SupportRequestController CreateController(ISupportRequestService supportRequestService, IUserService userService, User admin)
        {
            var controller = new Mock<SupportRequestController>(supportRequestService, userService);
            controller.CallBase = true;
            controller.Object.SetOwinContextOverride(Fakes.CreateOwinContext());
            TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), controller.Object);
            controller.Object.SetCurrentUser(admin);
            return controller.Object;
        }
    }
}
