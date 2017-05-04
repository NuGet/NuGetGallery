// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// View model for the security policies admin view.
    /// </summary>
    public class SecurityPolicyViewModel
    {
        /// <summary>
        /// Users search query.
        /// </summary>
        public string UsersQuery { get; set; }

        /// <summary>
        /// Available security policy groups.
        /// </summary>
        public IEnumerable<string> SubscriptionNames { get; set; }

        /// <summary>
        /// User subscription requests, in JSON format.
        /// </summary>
        public IEnumerable<string> UserSubscriptions { get; set; }
    }
}