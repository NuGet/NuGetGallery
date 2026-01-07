// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// Security policy group subscriptions for a user.
    /// </summary>
    public class UserSecurityPolicySubscriptions
    {
        public int UserId { get; set; }

        public string Username { get; set; }

        /// <summary>
        /// Dictionary of security policy subscriptions, and whether user is subscribed.
        /// </summary>
        public IDictionary<string, bool> Subscriptions { get; set; }
    }
}