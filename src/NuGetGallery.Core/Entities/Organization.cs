// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// Organization <see cref="NuGetGallery.User" /> account, based on TPT hierarchy.
    /// 
    /// With the addition of organizations, the users table effectively becomes an account table. Organizations accounts
    /// are child types created using TPT inheritance. User accounts are not child types, but this could be done in the
    /// future if we want to add constraints for user accounts or user-only settings.
    /// </summary>
    /// <see href="https://weblogs.asp.net/manavi/inheritance-mapping-strategies-with-entity-framework-code-first-ctp5-part-2-table-per-type-tpt" />
    public class Organization : User
    {
        public Organization() : base()
        {
            Members = new List<Membership>();
        }

        public Organization(string name) : base(name)
        {
            Members = new List<Membership>();
        }

        [StringLength(maximumLength: 256)]
        public string TenantId { get; set; }

        /// <summary>
        /// Organization Memberships to this organization.
        /// </summary>
        public virtual ICollection<Membership> Members { get; set; }
    }
}
