// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class OrganizationExtensions
    {
        public static Membership GetMembershipOfUser(this Organization organization, User member)
        {
            return organization.Members.FirstOrDefault(m => m.Member.MatchesUser(member));
        }

        /// <summary>
        /// Returns all the user accounts that are members of an organization.
        /// If the organization has nested organizations their members will be returned as well.
        /// The result will not have duplicate elements.
        /// 
        /// Nested organizations (teams) are not supported in the Gallery yet, but this method allows for it in case we lift that constraint.
        /// </summary>
        /// <param name="organization">The organization.</param>
        /// <returns>The <see cref="IEnumerable{User}"/> of users that are not <see cref="Organization"/> and are members of <paramref name="organization"/>.</returns>
        public static IEnumerable<User> GetUserAccountMembers(this Organization organization)
        {
            Queue<Organization> organizations = new Queue<Organization>();
            organizations.Enqueue(organization);
            HashSet<User> distinctUsers = new HashSet<User>();
            while (organizations.Any())
            {
                var currentOrganization = organizations.Dequeue();
                foreach (var membership in currentOrganization.Members)
                {
                    if (membership.Member is Organization)
                    {
                        organizations.Enqueue((Organization)membership.Member);
                    }
                    else
                    {
                        distinctUsers.Add(membership.Member);
                    }
                }
            }

            return distinctUsers;
        }
    }
}