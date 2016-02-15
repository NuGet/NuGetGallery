// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin
{
    public class SupportRequestService
        : ISupportRequestService
    {
        private readonly ISupportRequestDbContext _supportRequestDbContext;
        private const string _unassignedAdmin = "unassigned";

        public SupportRequestService(ISupportRequestDbContext supportRequestDbContext)
        {
            _supportRequestDbContext = supportRequestDbContext;
        }

        public IReadOnlyCollection<Models.Admin> GetAllAdmins()
        {
            return _supportRequestDbContext.Admins.ToList();
        }

        public int? GetAdminKeyFromUsername(string username)
        {
            if (string.Equals(username, _unassignedAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var admin = _supportRequestDbContext.Admins.FirstOrDefault(a => username == a.PagerDutyUsername);
            return admin?.Key;
        }

        public List<History> GetHistoryEntriesByIssueKey(int id)
        {
            return _supportRequestDbContext.Histories.Where(h => h.IssueId == id).ToList();
        }

        public IReadOnlyCollection<Issue> GetIssues(int ? assignedTo = null, string reason = null, int? issueStatusId = null)
        {
            var queryable = GetFilteredIssuesQueryable(assignedTo, reason, issueStatusId);

            return queryable.ToList();
        }

        public int GetIssueCount(int? assignedToId, string reason, int? issueStatusId)
        {
            return GetFilteredIssuesQueryable(assignedToId, reason, issueStatusId).Count();
        }

        public async Task UpdateAdminAsync(int adminId, string galleryUsername, string pagerDutyUsername)
        {
            if (string.IsNullOrEmpty(galleryUsername))
            {
                throw new ArgumentException(nameof(galleryUsername));
            }

            if (string.IsNullOrEmpty(pagerDutyUsername))
            {
                throw new ArgumentException(nameof(pagerDutyUsername));
            }

            var admin = GetAdminByKey(adminId);
            if (admin == null)
            {
                throw new ArgumentOutOfRangeException(nameof(adminId));
            }

            admin.GalleryUsername = galleryUsername;
            admin.PagerDutyUsername = pagerDutyUsername;

            await _supportRequestDbContext.CommitChangesAsync();
        }

        public async Task AddAdminAsync(string galleryUsername, string pagerDutyUsername)
        {
            if (string.IsNullOrEmpty(galleryUsername))
            {
                throw new ArgumentException(nameof(galleryUsername));
            }

            if (string.IsNullOrEmpty(pagerDutyUsername))
            {
                throw new ArgumentException(nameof(pagerDutyUsername));
            }

            var admin = new Models.Admin();
            admin.PagerDutyUsername = pagerDutyUsername;
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
                    var previousAssignedUsername = currentIssue.AssignedTo?.GalleryUsername ?? "unassigned";
                    var newAssignedUsername = assignedToId.HasValue ? GetAdminByKey(assignedToId.Value).GalleryUsername : "unassigned";

                    comments += $"Reassigned issue from '{previousAssignedUsername}' to '{newAssignedUsername}'.\r\n";

                    currentIssue.AssignedToId = assignedToId;
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
                        AssignedToId = assignedToId,
                        EntryDate = DateTime.UtcNow,
                        IssueStatusId = issueStatusId,
                        Comments = comments
                    };

                    await AddHistoryRecordAsync(history);
                }
            }
        }

        public async Task AddIssueAsync(Issue issue)
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

        private IQueryable<Issue> GetFilteredIssuesQueryable(int? assignedTo = null, string reason = null, int? issueStatusId = null)
        {
            IQueryable<Issue> queryable = _supportRequestDbContext.Issues;

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
                queryable = queryable.Where(r => r.IssueStatusId == issueStatusId);
            }

            return queryable;
        }

        private Models.Admin GetAdminByKey(int key)
        {
            return _supportRequestDbContext.Admins.FirstOrDefault(a => a.Key == key);
        }

        private Issue GetIssueById(int id)
        {
            return _supportRequestDbContext.Issues.FirstOrDefault(i => i.Key == id);
        }
    }
}
