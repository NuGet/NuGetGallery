// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.SupportRequest
{
    public interface ISupportRequestService
    {
        List<IssueStatus> GetAllIssueStatuses();
        string GetIssueStatusNameById(int id);

        /// <summary>
        /// Gets the <see cref="Issue"/> elements matching the provided filter parameters.
        /// </summary>
        /// <param name="assignedTo">
        /// Filter on AssignedTo.<code>null</code> means any value, -1 means 'unassigned' values.
        /// Default value is <code>null</code>.
        /// </param>
        /// <param name="reason">
        /// Filter on Reason. <code>null</code> means any value.
        /// Default value is <code>null</code>.
        /// </param>
        /// <param name="issueStatusId">
        /// Filter on IssueStatus. <code>null</code> means any value.
        /// Default value is <code>null</code>.
        /// </param>
        /// <param name="galleryUsername">
        /// Allows ordering by gallery username, showing issues matching the provided value first.
        /// </param>
        /// <returns>Returns a <see cref="IReadOnlyCollection{Issue}"/> that matches the provided filter parameters.</returns>
        IReadOnlyCollection<Issue> GetIssues(int? assignedTo = null, string reason = null, int? issueStatusId = null, string galleryUsername = null);

        Task<Issue> AddNewSupportRequestAsync(
            string subject, 
            string message,
            string requestorEmailAddress,
            string reason,
            User user,
            Package package = null);

        Task UpdateIssueAsync(int issueId, int? assignedToId, int issueStatusId, string comment, string editedBy);

        int GetIssueCount(int? assignedToId, string reason, int? issueStatusId);

        List<History> GetHistoryEntriesByIssueKey(int id);
        Task AddHistoryRecordAsync(History entry);

        IReadOnlyCollection<Admin> GetAllAdmins();

        Task ToggleAdminAccessAsync(int adminId, bool enabled);

        Task UpdateAdminAsync(int adminId, string galleryUsername);

        Task AddAdminAsync(string galleryUsername);

        Task DeleteSupportRequestsAsync(User user);

        Task<bool> TryAddDeleteSupportRequestAsync(User user);
    }
}