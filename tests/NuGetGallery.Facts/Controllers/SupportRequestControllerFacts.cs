// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Moq;
using Xunit;
using System.Net.Mail;
using NuGetGallery.Framework;
using NuGetGallery.Authentication;
using Microsoft.Owin;
using System.Threading.Tasks;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Configuration;
using System.Security.Claims;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using System.Data.Entity;

namespace NuGetGallery
{
    public class SupportRequestControllerFacts
    {
        private FakeSupportRequestDbContext CreateBasicTestContext()
        {
            var context = new FakeSupportRequestDbContext
            {
                Admins =
                 {
                    new Admin { UserName = "ranjinim"},
                    new Admin { UserName = "nugettest"},
                    new Admin { UserName = "nugetcore"},
                 },
                IssueStatus =
                {
                    new IssueStatus { StatusName = "New" },
                    new IssueStatus {StatusName = "Working" }
                }
            };
            return context;
        }

        private Issue CreateBasicTestIssue()
        {
            var newIssue = new Issue
            {
                AssignedTo = 1,
                Comments = "Testing create",
                CreatedBy = "test@nuget.org",
                CreatedDate = DateTime.UtcNow,
                Details = "Packge contains malicious code",
                IssueStatus = 1,
                IssueTitle = "Test create",
                OwnerEmail = "owner@nuget.org",
                PackageID = "jquery",
                PackageVersion = "1.2",
                Reason = "Other",
                SiteRoot = "https://nuget.test.org/"
            };

            return newIssue;
        }

        private FakeSupportRequestDbContext CreateContextWithListOfTestIssues()
        {
            var context = new FakeSupportRequestDbContext
            {
                Issues =
                 {
                    new Issue
            {
                AssignedTo = 1,
                Comments = "Testing create",
                CreatedBy = "test@nuget.org",
                CreatedDate = DateTime.UtcNow,
                Details = "Packge contains malicious code",
                IssueStatus = 1,
                IssueTitle = "Test create",
                OwnerEmail = "owner@nuget.org",
                PackageID = "jquery",
                PackageVersion = "1.2",
                Reason = "Other",
                SiteRoot = "https://nuget.test.org/"
            },
                    new Issue
            {
                AssignedTo = 3,
                Comments = "Testing create",
                CreatedBy = "test@nuget.org",
                CreatedDate = DateTime.UtcNow,
                Details = "Packge contains malicious code",
                IssueStatus = 1,
                IssueTitle = "Test create",
                OwnerEmail = "owner@nuget.org",
                PackageID = "jquery",
                PackageVersion = "1.2",
                Reason = "PrivateData",
                SiteRoot = "https://nuget.test.org/"
            },
                   new Issue
            {
                AssignedTo = 3,
                Comments = "Testing create",
                CreatedBy = "test@nuget.org",
                CreatedDate = DateTime.UtcNow,
                Details = "Packge contains malicious code",
                IssueStatus = 3,
                IssueTitle = "Test create",
                OwnerEmail = "owner@nuget.org",
                PackageID = "jquery",
                PackageVersion = "1.2",
                Reason = "MaliciousCode",
                SiteRoot = "https://nuget.test.org/"
            }
                 }
            };
            return context;

        }

        [Fact]
        public void VerifyCreatingAnIssueAddsANewIssueAndAddsAHistory()
        {
            //Arrange
            var context = CreateBasicTestContext();
            var newIssue = CreateBasicTestIssue();

            var createViewModel = new CreateViewModel
            {
                Issue = newIssue
            };

            var controller = new SupportRequestController(context);
            var prevIssueCount = context.GetIssueCount();
            var prevHistoryCount = context.GetHistoryCount();

            //Act
            controller.Create(createViewModel);

            //Assert   
            Assert.Equal(context.GetIssueCount(), prevIssueCount + 1);
            Assert.Equal(context.GetHistoryCount(), prevHistoryCount + 1);
        }

        [Fact]
        public void VerifyEditingAnIssueUpdatesTheIssueAndAddsAHistory()
        {
            //Arrange
            var context = CreateBasicTestContext();
            var newIssue = CreateBasicTestIssue();

            context.Issues.Add(newIssue);

            var editViewModel = new EditViewModel
            {
                Issue = newIssue,
            };

            var controller = new SupportRequestController(context);
            var prevAssignedTo = context.GetAssignedTo(0);
            var prevIssueStatus = context.GetIssueStatus(0);
            var prevIssueCount = context.GetIssueCount();
            var prevHistoryCount = context.GetHistoryCount();

            editViewModel.Issue.AssignedTo = 2;
            editViewModel.Issue.IssueStatus = 2;

            //Act
            controller.Edit(editViewModel);

            //Assert   
            Assert.Equal(prevAssignedTo, 1);
            Assert.Equal(prevIssueStatus, 1);
            Assert.Equal(prevIssueCount, 1);
            Assert.Equal(prevHistoryCount, 0);

            Assert.Equal(context.GetAssignedTo(0), 2);
            Assert.Equal(context.GetIssueStatus(0), 2);
            Assert.Equal(context.GetIssueCount(), prevIssueCount);
            Assert.Equal(context.GetHistoryCount(), prevHistoryCount + 1);
        }

        [Fact]
        public void VerifyFilteringIssueOnASingleFilter()
        {
            //Test on AssignedToFilter
            //Arrange
           var context = CreateContextWithListOfTestIssues();
           var controller = new SupportRequestController(context);

            //Act
            var result = controller.index(assignedToFilter: 3);
            var model = (IndexViewModel) result.Model;

            //Assert
            context.VerifyAssignedToInFilteredIssuesList(model, 3, 2);

            //Test on IssueStatusNameFilter
            //Arrange
            context = CreateContextWithListOfTestIssues();
            controller = new SupportRequestController(context);

            //Act
            result = controller.index(issueStatusNameFilter: 3);
            model = (IndexViewModel)result.Model;

            //Assert
            context.VerifyIssueStatusInFilteredIssuesList(model, 3, 1);

            //Test on IssueStatusNameFilter
            //Arrange
            context = CreateContextWithListOfTestIssues();
            controller = new SupportRequestController(context);

            //Act
            result = controller.index(reasonFilter: "MaliciousCode");
            model = (IndexViewModel)result.Model;

            //Assert
            context.VerifyReasonInFilteredIssuesList(model, "MaliciousCode", 1);
        }

        [Fact]
        public void VerifyFilteringIssueOnAMultipleFilters()
        {
            //Test on AssignedToFilter and IssueStatusNameFilter
            //Arrange
            var context = CreateContextWithListOfTestIssues();
            var controller = new SupportRequestController(context);

            //Act
            var result = controller.index(assignedToFilter: 3, issueStatusNameFilter: 1);
            var model = (IndexViewModel)result.Model;

            //Assert
            context.VerifyAssignedToInFilteredIssuesList(model, 3, 1);
            context.VerifyIssueStatusInFilteredIssuesList(model, 1, 1);

            //Test on AssignedToFilter, IssueStatusNameFilter and ReasonFilter
            //Arrange
            context = CreateContextWithListOfTestIssues();
            controller = new SupportRequestController(context);

            //Act
            result = controller.index(assignedToFilter:3, issueStatusNameFilter: 1, reasonFilter: "PrivateData");
            model = (IndexViewModel)result.Model;

            //Assert
            context.VerifyAssignedToInFilteredIssuesList(model, 3, 1);
            context.VerifyIssueStatusInFilteredIssuesList(model, 1, 1);
            context.VerifyReasonInFilteredIssuesList(model, "PrivateData", 1);
        }

        public class FakeSupportRequestDbContext : ISupportRequestDbContext
        {
            public FakeEntitiesContext fakeEntitiesContext = new FakeEntitiesContext();

            public FakeSupportRequestDbContext()
            {
                this.Admins = new FakeDbSet<Admin>(fakeEntitiesContext);
                this.IssueStatus = new FakeDbSet<IssueStatus>(fakeEntitiesContext);
                this.Issues = new FakeDbSet<Issue>(fakeEntitiesContext);
                this.Histories = new FakeDbSet<History>(fakeEntitiesContext);
            }

            public IDbSet<Admin> Admins { get; set; }


            public IDbSet<History> Histories { get; set; }

            public IDbSet<Issue> Issues { get; set; }

            public IDbSet<IssueStatus> IssueStatus { get; set; }

            public void CommitChanges()
            {
                fakeEntitiesContext.SaveChanges();
            }

            public int GetIssueCount()
            {
                return Issues.Local.Count;
            }

            public int GetIssueCount(IDbSet<Issue> issues)
            {
                return issues.Local.Count;
            }

            public int GetHistoryCount()
            {
                return Histories.Local.Count;
            }

            public int? GetAssignedTo(int index)
            {
                return Issues.Local[index].AssignedTo;
            }

            public int? GetIssueStatus(int index)
            {
                return Issues.Local[index].IssueStatus;
            }

            public void VerifyAssignedToInFilteredIssuesList(IndexViewModel ivm, int expectedAssignedToValue, int expectedCount)
            {
                var count = 0;
                foreach (var m in ivm.Issues)
                {
                    Assert.Equal(m.Issue.AssignedTo, expectedAssignedToValue);
                    count++;
                }
                Assert.Equal(count, expectedCount);
            }

            public void VerifyIssueStatusInFilteredIssuesList(IndexViewModel ivm, int expectedIssueStatusValue, int expectedCount)
            {
                var count = 0;
                foreach (var m in ivm.Issues)
                {
                    Assert.Equal(m.Issue.IssueStatus, expectedIssueStatusValue);
                    count++;
                }
                Assert.Equal(count, expectedCount);
            }

            public void VerifyReasonInFilteredIssuesList(IndexViewModel ivm, string expectedReasonValue, int expectedCount)
            {
                var count = 0;
                foreach (var m in ivm.Issues)
                {
                    Assert.Equal(m.Issue.Reason, expectedReasonValue);
                    count++;
                }
                Assert.Equal(count, expectedCount);
            }
        }
    }
}