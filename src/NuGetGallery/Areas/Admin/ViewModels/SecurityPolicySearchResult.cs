// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// User search results for the security policies admin view.
    /// </summary>
    public class SecurityPolicySearchResult
    {
        /// <summary>
        /// Found users, with security policy group enrollments.
        /// </summary>
        public IEnumerable<SecurityPolicyEnrollments> Users { get; set; }

        /// <summary>
        /// Usernames not found in the database.
        /// </summary>
        public IEnumerable<string> UsersNotFound { get; set; }
    }

    /// <summary>
    /// Security policy group enrollments for a user.
    /// </summary>
    public class SecurityPolicyEnrollments
    {
        public int UserId { get; set; }
        
        public string Username { get; set; }

        /// <summary>
        /// Dictionary of security policy group names (key) and whether the user is enrolled. 
        /// </summary>
        public IDictionary<string, bool> Enrollments { get; set; }
    }
}