﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class SupportRequestService 
        : ISupportRequestService
    {
        private readonly ISupportRequestDbContext _supportRequestContext;

        const string UnassignedAdmin = "unassigned";
        const int PackageDeletedResolution = 5;
        const int WorkAroundProvidedResolution = 6;
        const int NewIssueStatus = 1;
        const string NewUser = "new";

        public SupportRequestService(
           ISupportRequestDbContext supportRequestContext)
        {
            _supportRequestContext = supportRequestContext;
        }

        public List<Admin> GetAllAdmins()
        {
            //Return only active admins. AdminStatus is 1 for active admins
            return (from a in _supportRequestContext.Admins
                    where (a.AdminStatus == 1)
                    select a).ToList();
        }

        public List<String> GetAllAdminGalleryUserNames()
        {
            return (from a in _supportRequestContext.Admins
                    select (a.GalleryUserName)).ToList();
        }

        public string GetGalleryUserNameById(int id)
        {
            var name = from a in _supportRequestContext.Admins
                       where a.Key == id
                       select (a.GalleryUserName);

            if (name.Any())
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

            if (id.Any())
            {
                return id.First();
            }
            else
            {
                return -1;
            }
        }

        public int GetAdminKeyFromGalleryUserName(string userName)
        {
            var id = from a in _supportRequestContext.Admins
                     where userName.Equals(a.GalleryUserName, StringComparison.OrdinalIgnoreCase)
                     select (a.Key);

            if (id.Any())
            {
                return id.First();
            }
            else
            {
                return -1;
            }
        }

        public List<History> GetHistoryEntriesByIssueKey(int id)
        {
            var entries = from a in _supportRequestContext.Histories
                          where a.IssueKey == id
                          select a;

            if (entries.Any())
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
            _supportRequestContext.CommitChanges();
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
            var id = GetAdminKeyFromUserName(UnassignedAdmin);
            var allUnassignedIssues = from r in _supportRequestContext.Issues
                                      where (r.AssignedTo == id)
                                      select r;
            return allUnassignedIssues.ToList();
        }

        public int GetCountOfUnassignedIssues()
        {
            var id = GetAdminKeyFromUserName(UnassignedAdmin);
            var allIssues = from r in _supportRequestContext.Issues
                            where (r.AssignedTo == id)
                            select r;
            
            return allIssues.Count();
        }

        public int GetCountOfIssuesAssignedToAnAdmin(int adminKey)
        {
            var issues = from r in GetAllIssues()
                         where (r.AssignedTo == adminKey)
                         select r;
            return issues.Count();
        }

        public List<Issue> GetIssuesAssignedToMe(string galleryUserName)
        {
            var openIssues = GetOpenIssues();
            var adminId = GetAdminKeyFromGalleryUserName(galleryUserName);
            var myIssues = from r in openIssues
                                      where (r.AssignedTo == adminId)
                                      select r;
            return myIssues.ToList();
        }

        public int GetCountOfMyIssues(string galleryUserName)
        {
            var adminId = GetAdminKeyFromGalleryUserName(galleryUserName);
            var myIssues = from r in _supportRequestContext.Issues
                           where (r.AssignedTo == adminId)
                           select r;

            return myIssues.Count();
        }

        public Issue GetIssueById(int id)
        {
            var issue = from r in _supportRequestContext.Issues
                        where (r.Key == id)
                        select r;

            if (issue.Any())
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
            _supportRequestContext.CommitChanges();

            AddHistoryEntry(newIssue, loggedInUser);
        }

        public void AddAdmin(Admin admin)
        {
            admin.AdminStatus = 1;
            _supportRequestContext.Admins.Add(admin);
            _supportRequestContext.CommitChanges();
        }

        public SupportRequestDeleteAdminResult InactivateAdmin(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                var id = GetAdminKeyFromUserName(userName.Trim());
                var admin = _supportRequestContext.Admins.Find(id);
                var issueCount = GetCountOfIssuesAssignedToAnAdmin(id);

                if (issueCount > 0)
                {
                    return SupportRequestDeleteAdminResult.AdminHasAssignedIssues;
                }

                if (admin != null)
                {
                    //Change the admin status to 0 instead of deleting the admin
                    //this is because the admin key is associated with history entries
                    //By setting to zero, we will filter it out from showing in the UI
                    admin.AdminStatus = 0;
                    _supportRequestContext.CommitChanges();
                    return SupportRequestDeleteAdminResult.DeleteSuccessful;
                }
                else
                {
                    return SupportRequestDeleteAdminResult.AdminNotPresent;
                }
            }
            return SupportRequestDeleteAdminResult.EmptyUserName;
        }

        public void AddHistoryEntry(Issue newIssue, string loggedInUser)
        {
            var newEntry = new History();
            newEntry.EntryDate = DateTime.UtcNow;
            newEntry.AssignedTo = GetGalleryUserNameById(newIssue.AssignedTo ?? GetAdminKeyFromUserName(UnassignedAdmin));
            newEntry.IssueStatus = GetIssueStatusNameById(newIssue.IssueStatus ?? NewIssueStatus);
            newEntry.IssueKey = newIssue.Key;
            newEntry.Comments = newIssue.Comments;
            if (!string.IsNullOrEmpty(loggedInUser) &&
                !string.Equals(loggedInUser, NewUser, StringComparison.OrdinalIgnoreCase))
            {
                newEntry.EditedBy = (GetAdminKeyFromGalleryUserName(loggedInUser) == -1) ? 0 :
                                        GetAdminKeyFromGalleryUserName(loggedInUser);
            }
            else
            {
                newEntry.EditedBy = 0;
            }
            _supportRequestContext.Histories.Add(newEntry);
            _supportRequestContext.CommitChanges();
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

            if (status.Any())
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

            if (id.Any())
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
