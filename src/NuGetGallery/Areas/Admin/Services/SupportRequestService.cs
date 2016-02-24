// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Gallery;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public class SupportRequestService
        : ISupportRequestService
    {
        private readonly ISupportRequestDbContext _supportRequestDbContext;
        private readonly PagerDutyClient _pagerDutyClient;
        private readonly string _siteRoot;
        private const string _unassignedAdmin = "unassigned";

        public SupportRequestService(
            ISupportRequestDbContext supportRequestDbContext,
            IAppConfiguration config)
        {
            _supportRequestDbContext = supportRequestDbContext;
            _siteRoot = config.SiteRoot;

            _pagerDutyClient = new PagerDutyClient(config.PagerDutyAccountName, config.PagerDutyAPIKey, config.PagerDutyServiceKey);
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

        public IReadOnlyCollection<Issue> GetIssues(int? assignedTo = null, string reason = null, int? issueStatusId = null, string galleryUsername = null)
        {
            var queryable = GetFilteredIssuesQueryable(assignedTo, reason, issueStatusId, galleryUsername);

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
                    string newAssignedUsername;
                    if (assignedToId.HasValue)
                    {
                        var admin = GetAdminByKey(assignedToId.Value);
                        if (admin == null)
                        {
                            newAssignedUsername = "unassigned";
                        }
                        else
                        {
                            newAssignedUsername = admin.GalleryUsername;
                            currentIssue.AssignedToId = assignedToId;
                        }
                    }
                    else
                    {
                        newAssignedUsername = "unassigned";
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

        public async Task AddNewSupportRequestAsync(string subject, string message, string requestorEmailAddress, string reason,
            User user, Package package = null)
        {
            var loggedInUser = user?.Username ?? "Anonymous";

            try
            {
                var newIssue = new Issue();

                // If primary on-call person is not yet configured in the Support Request DB, assign to 'unassigned'.
                var primaryOnCall = await _pagerDutyClient.GetPrimaryOnCallAsync();
                if (string.IsNullOrEmpty(primaryOnCall) || GetAdminKeyFromUsername(primaryOnCall) == -1)
                {
                    newIssue.AssignedTo = null;
                }
                else
                {
                    newIssue.AssignedToId = GetAdminKeyFromUsername(primaryOnCall);
                }

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
            }
            catch (SqlException sqlException)
            {
                QuietLog.LogHandledException(sqlException);

                var packageInfo = "N/A";
                if (package != null)
                {
                    packageInfo = $"{package.PackageRegistration.Id} v{package.Version}";
                }

                var errorMessage = $"Error while submitting support request at {DateTime.UtcNow}. User requesting support = {loggedInUser}. Support reason = {reason ?? "N/A"}. Package info = {packageInfo}";

                await _pagerDutyClient.TriggerIncidentAsync(errorMessage);
            }
            catch (Exception e) //In case getting data from PagerDuty has failed
            {
                QuietLog.LogHandledException(e);
            }
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
            return _supportRequestDbContext.Issues.FirstOrDefault(i => i.Key == id);
        }
    }
}
