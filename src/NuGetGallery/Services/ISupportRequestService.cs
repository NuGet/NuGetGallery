// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGetGallery.Packaging;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public interface ISupportRequestService
    {
        List<Admin> GetAllAdmins();
        List<String> GetAllAdminGalleryUserNames();
        string GetGalleryUserNameById(int id);
        int GetAdminKeyFromUserName(string userName);
        int GetAdminKeyFromGalleryUserName(string userName);
        List<History> GetHistoryEntriesByIssueKey(int id);
        void AddNewHistoryEntry(History newEntry);
        List<Issue> GetOpenIssues();
        List<Issue> GetResolvedIssues();
        List<Issue> GetAllIssues();
        List<Issue> GetUnassignedIssues();
        int GetCountOfUnassignedIssues();
        int GetCountOfOpenIssues();
        int GetCountOfResolvedIssues();
        Issue GetIssueById(int id);
        void AddIssue(Issue newIssue, string loggedInUser);
        void AddHistoryEntry(Issue newIssue, string loggedInUser);
        void AddAdmin(Admin admin);
        bool DeleteAdmin(string userName);
        List<IssueStatus> GetAllIssueStatuses();
        IssueStatus GetIssueStatusById(int id);
        string GetIssueStatusNameById(int id);
        int GetIssueStatusIdByName(string issueStatusName);
        List<Issue> GetIssuesAssignedToMe(string galleryUserName);
        int GetCountOfMyIssues(string galleryUserName);
    }
}