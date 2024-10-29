// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// User search results for the security policies admin view.
    /// </summary>
    public class UserSecurityPolicySearchResult
    {
        /// <summary>
        /// Found users, with security policy subscriptions they are subscribed to.
        /// </summary>
        public IEnumerable<UserSecurityPolicySubscriptions> Users { get; set; }

        /// <summary>
        /// Usernames not found in the database.
        /// </summary>
        public IEnumerable<string> UsersNotFound { get; set; }
    }
}