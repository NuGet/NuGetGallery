// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public class SupportRequestService
        : ISupportRequestService
    {
        private readonly ISupportRequestDbContext _supportRequestDbContext;
        private IAuditingService _auditingService;
        private readonly string _siteRoot;
        private const string _unassignedAdmin = "unassigned";
        private const string _deletedAccount = "_deletedaccount";
        private const string _NuGetDSRAccount = "_NuGetDSR";

        public SupportRequestService(
            ISupportRequestDbContext supportRequestDbContext,
            IAppConfiguration config,
            IAuditingService auditingService)
        {
            _supportRequestDbContext = supportRequestDbContext;
            _siteRoot = config.SiteRoot;
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public IReadOnlyCollection<Models.Admin> GetAllAdmins()
        {
            return _supportRequestDbContext.Admins.ToList();
        }

        public List<History> GetHistoryEntriesByIssueKey(int id)
        {
            return _supportRequestDbContext.Histories.Where(h => h.IssueId == id).ToList();
        }

        public IReadOnlyCollection<Issue> GetIssues(int? assignedTo = null, string reason = null, int? issueStatusId = null, string galleryUsername = null)
        {
            var queryable = GetFilteredIssuesQueryable(assignedTo, reason, issueStatusId, galleryUsername);

            return queryable.ToList();
        }

        public int GetIssueCount(int? assignedToId, string reason, int? issueStatusId)
        {
            return GetFilteredIssuesQueryable(assignedToId, reason, issueStatusId).Count();
        }

        public async Task UpdateAdminAsync(int adminId, string galleryUsername)
        {
            if (string.IsNullOrEmpty(galleryUsername))
            {
                throw new ArgumentException(nameof(galleryUsername));
            }

            var admin = GetAdminByKey(adminId);
            if (admin == null)
            {
                throw new ArgumentOutOfRangeException(nameof(adminId));
            }

            admin.GalleryUsername = galleryUsername;

            await _supportRequestDbContext.CommitChangesAsync();
        }

        public async Task AddAdminAsync(string galleryUsername)
        {
            if (string.IsNullOrEmpty(galleryUsername))
            {
                throw new ArgumentException(nameof(galleryUsername));
            }

            var admin = new Models.Admin();
            admin.GalleryUsername = galleryUsername;

            _supportRequestDbContext.Admins.Add(admin);

            await _supportRequestDbContext.CommitChangesAsync();
        }

        public async Task UpdateIssueAsync(int issueId, int? assignedToId, int issueStatusId, string comment, string editedBy)
        {
            if (string.IsNullOrEmpty(editedBy))
            {
                throw new ArgumentNullException(nameof(editedBy));
            }

            var currentIssue = GetIssueById(issueId);

            if (currentIssue == null)
            {
                throw new ArgumentOutOfRangeException(nameof(editedBy));
            }
            else
            {
                var changesDetected = false;

                var comments = comment?.Trim();
                if (!string.IsNullOrEmpty(comments))
                {
                    comments += "\r\n";
                }

                if (currentIssue.AssignedToId != assignedToId)
                {
                    var previousAssignedUsername = currentIssue.AssignedTo?.GalleryUsername ?? _unassignedAdmin;
                    string newAssignedUsername;
                    if (assignedToId.HasValue)
                    {
                        var admin = GetAdminByKey(assignedToId.Value);
                        if (admin == null)
                        {
                            newAssignedUsername = _unassignedAdmin;
                        }
                        else
                        {
                            newAssignedUsername = admin.GalleryUsername;
                            currentIssue.AssignedToId = assignedToId;
                        }
                    }
                    else
                    {
                        newAssignedUsername = _unassignedAdmin;
                    }

                    comments += $"Reassigned issue from '{previousAssignedUsername}' to '{newAssignedUsername}'.\r\n";

                    currentIssue.AssignedTo = null;

                    changesDetected = true;
                }

                if (currentIssue.IssueStatusId != issueStatusId)
                {
                    var previousIssueStatusName = currentIssue.IssueStatus.Name;
                    var newIssueStatusName = GetIssueStatusNameById(issueStatusId);

                    currentIssue.IssueStatusId = issueStatusId;
                    currentIssue.IssueStatus = null;

                    comments += $"Updated issue status from '{previousIssueStatusName}' to '{newIssueStatusName}'.\r\n";

                    changesDetected = true;
                }

                if (!changesDetected && !string.IsNullOrEmpty(comments))
                {
                    changesDetected = true;
                }

                if (changesDetected)
                {
                    await _supportRequestDbContext.CommitChangesAsync();

                    var history = new History
                    {
                        IssueId = issueId,
                        EditedBy = editedBy,
                        AssignedToId = assignedToId == -1 ? null : assignedToId,
                        EntryDate = DateTime.UtcNow,
                        IssueStatusId = issueStatusId,
                        Comments = comments
                    };

                    await AddHistoryRecordAsync(history);
                }
            }
        }

        public async Task<Issue> AddNewSupportRequestAsync(
            string subject,
            string message,
            string requestorEmailAddress,
            string reason,
            User user,
            Package package = null)
        {
            var loggedInUser = user?.Username ?? "Anonymous";

            try
            {
                var newIssue = new Issue();

                newIssue.CreatedDate = DateTime.UtcNow;
                newIssue.Details = message;
                newIssue.IssueStatusId = IssueStatusKeys.New;
                newIssue.IssueTitle = subject;

                newIssue.CreatedBy = loggedInUser;
                newIssue.OwnerEmail = requestorEmailAddress;
                newIssue.PackageId = package?.PackageRegistration.Id;
                newIssue.PackageVersion = package?.Version;
                newIssue.Reason = reason;
                newIssue.SiteRoot = _siteRoot;
                newIssue.UserKey = user?.Key;
                newIssue.PackageRegistrationKey = package?.PackageRegistrationKey;

                await AddIssueAsync(newIssue);
                return newIssue;
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
            }

            return null;
        }

        public async Task<bool> TryAddDeleteSupportRequestAsync(User user)
        {
            var requestSent = await AddNewSupportRequestAsync(
                ServicesStrings.AccountDelete_SupportRequestTitle,
                ServicesStrings.AccountDelete_SupportRequestTitle,
                user.EmailAddress,
                "The user requested to have the account deleted.",
                user) != null;
            var status = requestSent ? DeleteAccountAuditRecord.ActionStatus.Success : DeleteAccountAuditRecord.ActionStatus.Failure;
            await _auditingService.SaveAuditRecordAsync(new DeleteAccountAuditRecord(username: user.Username,
                   status: status,
                   action: AuditedDeleteAccountAction.RequestAccountDeletion));

            return requestSent;
        }

        private async Task AddIssueAsync(Issue issue)
        {
            _supportRequestDbContext.Issues.Add(issue);

            await _supportRequestDbContext.CommitChangesAsync();

            var historyEntry = new History
            {
                EntryDate = DateTime.UtcNow,
                IssueId = issue.Key,
                AssignedToId = issue.AssignedToId.HasValue ? issue.AssignedToId : null,
                IssueStatusId = issue.IssueStatusId,
                Comments = "New Issue Created",
                EditedBy = issue.CreatedBy
            };

            await AddHistoryRecordAsync(historyEntry);
        }

        public async Task ToggleAdminAccessAsync(int adminId, bool enabled)
        {
            var admin = GetAdminByKey(adminId);
            if (admin == null)
            {
                throw new ArgumentOutOfRangeException(nameof(adminId));
            }

            admin.AccessDisabled = !enabled;

            await _supportRequestDbContext.CommitChangesAsync();
        }

        public async Task AddHistoryRecordAsync(History entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _supportRequestDbContext.Histories.Add(entry);

            await _supportRequestDbContext.CommitChangesAsync();
        }

        public List<IssueStatus> GetAllIssueStatuses()
        {
            return _supportRequestDbContext.IssueStatus.ToList();
        }

        public string GetIssueStatusNameById(int id)
        {
            var issue = _supportRequestDbContext.IssueStatus.FirstOrDefault(i => i.Key == id);

            return issue?.Name;
        }

        public async Task DeleteSupportRequestsAsync(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            var userIssues = GetIssues().Where(i => i.UserKey.HasValue && i.UserKey.Value == user.Key).ToList();
            // Delete all the support requests with exception of the delete account request.
            // For the DeleteAccount support request clean the user data.
            foreach (var issue in userIssues.Where(i => !string.Equals(i.IssueTitle, ServicesStrings.AccountDelete_SupportRequestTitle)))
            {
                _supportRequestDbContext.Issues.Remove(issue);
            }
            foreach (var accountDeletedIssue in userIssues.Where(i => string.Equals(i.IssueTitle, ServicesStrings.AccountDelete_SupportRequestTitle)))
            {
                accountDeletedIssue.OwnerEmail = "deletedaccount";
                if(!accountDeletedIssue.CreatedBy.Equals(_NuGetDSRAccount, StringComparison.OrdinalIgnoreCase))
                {
                    accountDeletedIssue.CreatedBy = _deletedAccount;
                }
                accountDeletedIssue.IssueStatusId = IssueStatusKeys.Resolved;
                accountDeletedIssue.Details = "This support request has been redacted as the customer's account has been deleted.";
                foreach (var historyEntry in accountDeletedIssue.HistoryEntries)
                {
                    if (string.Equals(historyEntry.EditedBy, user.Username, StringComparison.InvariantCultureIgnoreCase))
                    {
                        historyEntry.EditedBy = null;
                    }
                }
            }
            await _supportRequestDbContext.CommitChangesAsync();
        }

        private IQueryable<Issue> GetFilteredIssuesQueryable(int? assignedTo = null, string reason = null, int? issueStatusId = null, string galleryUsername = null)
        {
            IQueryable<Issue> queryable = _supportRequestDbContext.Issues
                .Include(i => i.HistoryEntries)
                .Include(i => i.IssueStatus)
                .Include(i => i.AssignedTo);

            if (assignedTo.HasValue && assignedTo.Value != -1)
            {
                queryable = queryable.Where(i => i.AssignedToId == assignedTo);
            }
            else if (assignedTo.HasValue && assignedTo.Value == -1)
            {
                // -1 equals UNASSIGNED issues (this to avoid a virtual Admin in the db...)
                // <null> equals ALL issues (so no need to filter)
                queryable = queryable.Where(i => i.AssignedToId == null);
            }

            if (!string.IsNullOrEmpty(reason))
            {
                queryable = queryable.Where(r => r.Reason.Equals(reason, StringComparison.OrdinalIgnoreCase));
            }

            if (issueStatusId.HasValue)
            {
                if (issueStatusId == IssueStatusKeys.Unresolved)
                {
                    queryable = queryable.Where(r => r.IssueStatusId < IssueStatusKeys.Resolved);
                }
                else
                {
                    queryable = queryable.Where(r => r.IssueStatusId == issueStatusId);
                }
            }

            // show current admin issues first,
            // then sort by issue status, showing new first, then working, then waiting for customer, then resolved,
            // then sort by creation time descending
            IOrderedQueryable<Issue> orderedQueryable;
            if (!string.IsNullOrEmpty(galleryUsername))
            {
                orderedQueryable = queryable
                    .OrderByDescending(i => i.AssignedTo.GalleryUsername == galleryUsername)
                    .ThenBy(i => i.IssueStatusId == IssueStatusKeys.New ? 1 : (i.IssueStatusId == IssueStatusKeys.Working ? 2 : (i.IssueStatusId == IssueStatusKeys.WaitingForCustomer ? 3 : 4)))
                    .ThenByDescending(i => i.CreatedDate);
            }
            else
            {
                orderedQueryable = queryable
                    .OrderBy(i => i.IssueStatusId == IssueStatusKeys.New ? 1 : (i.IssueStatusId == IssueStatusKeys.Working ? 2 : (i.IssueStatusId == IssueStatusKeys.WaitingForCustomer ? 3 : 4)))
                    .ThenByDescending(i => i.CreatedDate);
            }

            return orderedQueryable;
        }

        private Models.Admin GetAdminByKey(int key)
        {
            return _supportRequestDbContext.Admins.FirstOrDefault(a => a.Key == key);
        }

        private Issue GetIssueById(int id)
        {
            return _supportRequestDbContext
                .Issues
                .Include(x => x.IssueStatus)
                .FirstOrDefault(i => i.Key == id);
        }
    }
}
