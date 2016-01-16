// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Packaging;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public class SupportRequestService : ISupportRequestService
    {
        SupportRequest _supportRequestContext;

        const int UnassignedAdmin = 0;
        const int PackageDeletedResolution = 5;
        const int WorkAroundProvidedResolution = 6;
        const int NewIssueStatus = 1;
        const string NewUser = "new";

        public SupportRequestService(
           SupportRequest supportRequestContext)
        {
            _supportRequestContext = supportRequestContext;
        }

        protected virtual DbSet<Admin> Admins { get; set; }

        public List<Admin> GetAllAdmins()
        {
            return (from a in _supportRequestContext.Admins
                    select a).ToList();
        }

        public List<String> GetAllAdminUserNames()
        {
            return (from a in _supportRequestContext.Admins
                    select (a.UserName)).ToList();
        }

        public string GetUserNameById(int id)
        {
            var name = from a in _supportRequestContext.Admins
                       where a.Key == id
                       select (a.UserName);

            if (name != null && name.Count() > 0)
            {
                return name.First();
            }
            else
            {
                return null;
            }
        }

        public int GetAdminKeyFromUserName(string userName)
        {
            var id = from a in _supportRequestContext.Admins
                     where userName.Equals(a.UserName, StringComparison.OrdinalIgnoreCase)
                     select (a.Key);

            if (id != null && id.Count() > 0)
            {
                return id.First();
            }
            else
            {
                return 0;
            }
        }

        public List<History> GetHistoryEntriesByIssueKey(int id)
        {
            var entries = from a in _supportRequestContext.Histories
                          where a.IssueKey == id
                          select a;

            if (entries.Count() > 0)
            {
                return entries.ToList();
            }
            else
            {
                return null;
            }
        }

        public void AddNewHistoryEntry(History newEntry)
        {
            _supportRequestContext.Histories.Add(newEntry);
            _supportRequestContext.SaveChanges();
        }

        public List<Issue> GetOpenIssues()
        {
            var allOpenIssues = from r in _supportRequestContext.Issues
                                where (r.IssueStatus != PackageDeletedResolution
                                && r.IssueStatus != WorkAroundProvidedResolution)
                                select r;
            return allOpenIssues.ToList();
        }

        public List<Issue> GetResolvedIssues()
        {
            var allResolvedIssues = from r in _supportRequestContext.Issues
                                    where (r.IssueStatus == PackageDeletedResolution
                                    || r.IssueStatus == WorkAroundProvidedResolution)
                                    select r;
            return allResolvedIssues.ToList();
        }

        public List<Issue> GetAllIssues()
        {
            var allIssues = from r in _supportRequestContext.Issues
                            select r;
            return allIssues.ToList();
        }

        public List<Issue> GetUnassignedIssues()
        {
            var allUnassignedIssues = from r in _supportRequestContext.Issues
                                      where (r.AssignedTo == UnassignedAdmin)
                                      select r;
            return allUnassignedIssues.ToList();
        }

        public int GetCountOfUnassignedIssues()
        {
            var allIssues = from r in _supportRequestContext.Issues
                            where (r.AssignedTo == UnassignedAdmin)
                            select r;
            var count = 0;
            if (allIssues != null)
            {
                count = allIssues.Count();
            }
            return count;
        }

        public Issue GetIssueById(int id)
        {
            var issue = from r in _supportRequestContext.Issues
                        where (r.Key == id)
                        select r;
            if (issue.Count() > 0)
            {
                return issue.First();
            }
            else
            {
                return null;
            }
        }

        public int GetCountOfOpenIssues()
        {
            var count = (from r in _supportRequestContext.Issues
                         where (r.IssueStatus != PackageDeletedResolution
                         && r.IssueStatus != WorkAroundProvidedResolution)
                         select r).Count();
            return count;
        }

        public int GetCountOfResolvedIssues()
        {
            var count = (from r in _supportRequestContext.Issues
                         where (r.IssueStatus == PackageDeletedResolution
                         || r.IssueStatus == WorkAroundProvidedResolution)
                         select r).Count();
            return count;
        }

        public void AddIssue(Issue newIssue, string loggedInUser)
        {
            _supportRequestContext.Issues.Add(newIssue);
            _supportRequestContext.SaveChanges();

            AddHistoryEntry(newIssue, loggedInUser);
        }

        public void AddHistoryEntry(Issue newIssue, string loggedInUser)
        {
            var newEntry = new History();
            newEntry.EntryDate = DateTime.UtcNow;
            newEntry.AssignedTo = GetUserNameById(newIssue.AssignedTo ?? UnassignedAdmin);
            newEntry.IssueStatus = GetIssueStatusNameById(newIssue.IssueStatus ?? NewIssueStatus);
            newEntry.IssueKey = newIssue.Key;
            newEntry.Comments = newIssue.Comments;
            if (!string.IsNullOrEmpty(loggedInUser) &&
                !string.Equals(loggedInUser, NewUser, StringComparison.OrdinalIgnoreCase))
            {
                newEntry.EditedBy = GetAdminKeyFromUserName(loggedInUser);
            }
            else
            {
                newEntry.EditedBy = 0;
            }
            _supportRequestContext.Histories.Add(newEntry);
            _supportRequestContext.SaveChanges();
        }

        public List<IssueStatus> GetAllIssueStatuses()
        {
            var statuses = from s in _supportRequestContext.IssueStatus
                           select s;
            return statuses.ToList();
        }

        public IssueStatus GetIssueStatusById(int id)
        {
            var status = from r in _supportRequestContext.IssueStatus
                         where (r.Key == id)
                         select r;

            if (status.Count() > 0)
            {
                return status.First();
            }
            else
            {
                return null;
            }
        }

        public string GetIssueStatusNameById(int id)
        {
            var status = from r in _supportRequestContext.IssueStatus
                         where (r.Key == id)
                         select r.StatusName;

            if (status.Count() > 0)
            {
                return status.First();
            }
            else
            {
                return null;
            }
        }

        public int GetIssueStatusIdByName(string issueStatusName)
        {
            if (String.IsNullOrEmpty(issueStatusName))
                return -1;

            var id = from r in _supportRequestContext.IssueStatus
                     where (r.StatusName == issueStatusName)
                     select r.Key;

            if (id.Count() > 0)
            {
                return id.First();
            }
            else
            {
                return -1;
            }
        }
    }
}
