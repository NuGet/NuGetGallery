// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// The organization associated with a <see cref="NuGetGallery.User" /> account.
    /// 
    /// The Users table contains both User and Organization accounts. The Organizations table exists both
    /// as a constraint for Membership as well as a place for possible Organization-only settings. If User
    /// and Organization settings diverge, we can consider creating a separate UserSettings table as well.
    /// </summary>
    public class Organization
    {
        /// <summary>
        /// Organization primary key.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Organization account (User) foriegn key.
        /// </summary>
        public int AccountKey { get; set; }

        /// <summary>
        /// Organization account (User).
        /// </summary>
        public User Account { get; set; }

        /// <summary>
        /// Organization memberships.
        /// </summary>
        public virtual ICollection<Membership> Memberships { get; set; }
    }
}
