// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace NuGet.Services.Entities
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
        public Organization() : this(null)
        {
        }

        public Organization(string name) : base(name)
        {
            Members = new List<Membership>();
            MemberRequests = new List<MembershipRequest>();

            _administrators = new Lazy<IEnumerable<User>>(
                () => Members.Where(m => m.IsAdmin).Select(m => m.Member).ToList());
            _collaborators = new Lazy<IEnumerable<User>>(
                () => Members.Where(m => !m.IsAdmin).Select(m => m.Member).ToList());
        }

        /// <summary>
        /// Organization Memberships to this organization.
        /// </summary>
        public virtual ICollection<Membership> Members { get; set; }

        /// <summary>
        /// Requests to become a member of this <see cref="Organization"/>.
        /// </summary>
        public virtual ICollection<MembershipRequest> MemberRequests { get; set; }

        #region per-request query cache

        private Lazy<IEnumerable<User>> _administrators;
        private Lazy<IEnumerable<User>> _collaborators;

        [NotMapped]
        public IEnumerable<User> Administrators
        {
            get
            {
                return _administrators.Value;
            }
        }

        [NotMapped]
        public IEnumerable<User> Collaborators
        {
            get
            {
                return _collaborators.Value;
            }
        }

        #endregion

    }
}
