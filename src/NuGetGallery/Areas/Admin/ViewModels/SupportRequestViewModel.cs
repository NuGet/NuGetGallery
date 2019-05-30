// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class SupportRequestViewModel
    {
        public SupportRequestViewModel(Issue issue)
        {
            Key = issue.Key;
            CreatedBy = issue.CreatedBy;
            CreatedDate = issue.CreatedDate;
            IssueTitle = issue.IssueTitle;
            Details = issue.Details;
            SiteRoot = VerifyAndFixTrailingSlash(issue.SiteRoot);
            PackageId = issue.PackageId;
            PackageVersion = issue.PackageVersion;
            OwnerEmail = issue.OwnerEmail;
            Reason = issue.Reason;
            AssignedTo = issue.AssignedToId;
            AssignedToGalleryUsername = issue.AssignedTo == null ? "unassigned" : issue.AssignedTo.GalleryUsername;
            IssueStatusId = issue.IssueStatusId;
            PackageRegistrationKey = issue.PackageRegistrationKey;
            UserKey = issue.UserKey;
        }

        // Readonly fields
        public int Key { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string IssueTitle { get; set; }
        public string Details { get; set; }
        public string SiteRoot { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string OwnerEmail { get; set; }
        public string Reason { get; set; }
        public string AssignedToGalleryUsername { get; set; }
        public string IssueStatusName { get; set; }
        public string UserEmail { get; set; }
        public int? PackageRegistrationKey { get; set; }
        public int? UserKey { get; set; }
        public bool IsRelatedToPackage => !string.IsNullOrEmpty(PackageId) && !string.IsNullOrEmpty(PackageVersion);

        // Editable fields
        public int? AssignedTo { get; set; }
        public int IssueStatusId { get; set; }

        private static string VerifyAndFixTrailingSlash(string url)
        {
            var result = url;
            if (!string.IsNullOrEmpty(url) && url.Substring(url.Length - 1, 1) != "/")
            {
                result = string.Concat(url, "/");
            }
            return result;
        }
    }
}