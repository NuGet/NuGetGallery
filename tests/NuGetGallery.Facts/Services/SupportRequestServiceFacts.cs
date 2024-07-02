// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using Moq;
using Xunit;
using NuGet.Services.Entities;

namespace NuGetGallery.Services
{
    public class SupportRequestServiceFacts
    {
        public class TheDeleteSupportRequestsAsync
        {
            [Fact]
            public async Task DeleteRequestsNullInput()
            {
                // Arrange
                var auditingService = new Mock<IAuditingService>();
                TestSupportRequestDbContext supportRequestContext = new TestSupportRequestDbContext();
                SupportRequestService supportRequestService = new SupportRequestService(supportRequestContext, GetAppConfig(), auditingService.Object);

                // Act + Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => supportRequestService.DeleteSupportRequestsAsync(null));
            }

            [Fact]
            public async Task DeleteRequestsNormalPath()
            {
                // Arrange
                string userName = "Joe";
                string emailAddress = "Joe@coldmail.com";
                User user = new User()
                {
                    Username = userName,
                    EmailAddress = emailAddress,
                    Key = 11
                };

                TestSupportRequestDbContext supportRequestContext = new TestSupportRequestDbContext();
                Issue JoesDeleteAccountRequest = new Issue()
                {
                    CreatedBy = user.Username,
                    Key = 1,
                    IssueTitle = Strings.AccountDelete_SupportRequestTitle,
                    OwnerEmail = user.EmailAddress,
                    IssueStatusId = IssueStatusKeys.New,
                    HistoryEntries = new List<History>()
                    {
                        new History() { EditedBy = userName, IssueId = 1, Key = 1, IssueStatusId = IssueStatusKeys.New }
                    },
                    UserKey = user.Key
                };
                Issue JoesOldIssue = new Issue()
                {
                    CreatedBy = user.Username,
                    Key = 2,
                    IssueTitle = "Joe's OldIssue",
                    OwnerEmail = user.EmailAddress,
                    IssueStatusId = IssueStatusKeys.Resolved,
                    HistoryEntries = new List<History>()
                    {
                        new History() { EditedBy = userName, IssueId = 2, Key = 2, IssueStatusId = IssueStatusKeys.New },
                        new History() { EditedBy = userName, IssueId = 2, Key = 2, IssueStatusId = IssueStatusKeys.Resolved }
                    },
                    UserKey = user.Key
                };
                Issue randomIssue = new Issue()
                {
                    CreatedBy = $"{user.Username}_second",
                    Key = 3,
                    IssueTitle = "Second",
                    OwnerEmail = "random",
                    IssueStatusId = IssueStatusKeys.New,
                    HistoryEntries = new List<History>()
                    {
                        new History() { EditedBy = $"{userName}_second", IssueId = 3, Key = 3, IssueStatusId = IssueStatusKeys.New }
                    },
                    UserKey = user.Key + 1
                };
                supportRequestContext.Issues.Add(JoesDeleteAccountRequest);
                supportRequestContext.Issues.Add(JoesOldIssue);
                supportRequestContext.Issues.Add(randomIssue);

                var auditingService = new Mock<IAuditingService>();
                SupportRequestService supportRequestService = new SupportRequestService(supportRequestContext, GetAppConfig(), auditingService.Object);

                // Act
                await supportRequestService.DeleteSupportRequestsAsync(user);

                //Assert
                Assert.Equal<int>(2, supportRequestContext.Issues.Count());
                Assert.True(supportRequestContext.Issues.Any(issue => string.Equals(issue.CreatedBy, $"{userName}_second")));
                Assert.False(supportRequestContext.Issues.Any(issue => string.Equals(issue.IssueTitle, "Joe's OldIssue")));
                var deleteRequestIssue = supportRequestContext.Issues.FirstOrDefault(issue => issue.Key == 1);
                Assert.NotNull(deleteRequestIssue);
                Assert.Equal(deleteRequestIssue.CreatedBy, "_deletedaccount");
                Assert.Equal(deleteRequestIssue.IssueStatusId, IssueStatusKeys.Resolved);
                Assert.Null(deleteRequestIssue.HistoryEntries.ElementAt(0).EditedBy);
            }

            [Fact]
            public async Task DeleteAccountRequestWillAudit()
            {
                // Arrange
                var auditingService = new FakeAuditingService();
                TestSupportRequestDbContext supportRequestContext = new TestSupportRequestDbContext();
                SupportRequestService supportRequestService = new SupportRequestService(supportRequestContext, GetAppConfig(), auditingService);
                User user = new User("testUser");

                // Act
                await supportRequestService.TryAddDeleteSupportRequestAsync(user);

                // Assert
                Assert.Single(auditingService.Records);
                var deleteRecord = auditingService.Records[0] as DeleteAccountAuditRecord;
                Assert.True(deleteRecord != null);
                Assert.Equal(DeleteAccountAuditRecord.ActionStatus.Success, deleteRecord.Status);
            }
        }

        internal class FakeAuditingService : IAuditingService
        {
            public List<AuditRecord> Records = new List<AuditRecord>();

            public Task SaveAuditRecordAsync(AuditRecord record)
            {
                Records.Add(record);
                return Task.FromResult(true);
            }
        }

        static IAppConfiguration GetAppConfig()
        {
            var appConfig = new Mock<IAppConfiguration>();
            appConfig.Setup(m => m.SiteRoot).Returns("SiteRoot");

            return appConfig.Object;
        }

        internal class TestSupportRequestDbContext : ISupportRequestDbContext
        {
            public TestSupportRequestDbContext()
            {
                Admins = FakeEntitiesContext.CreateDbSet<Admin>();
                Issues = FakeEntitiesContext.CreateDbSet<Issue>();
                Histories = FakeEntitiesContext.CreateDbSet<History>();
                IssueStatus = FakeEntitiesContext.CreateDbSet<IssueStatus>();
            }

            public IDbSet<Admin> Admins { get; set; }
            public IDbSet<Issue> Issues { get; set; }
            public IDbSet<History> Histories { get; set; }
            public IDbSet<IssueStatus> IssueStatus { get; set; }

            public async Task CommitChangesAsync()
            {
                await Task.Yield();
                return;
            }
        }
    }
}