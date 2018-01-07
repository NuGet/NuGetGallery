// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class OrganizationExtensions
    {
        /// <summary>
        /// Returns all the user accounts that are members of an organization.
        /// If the orgnaization has other child organizations their members will be retuned as well. The result does not filter duplicate elements.
        /// </summary>
        /// <param name="organization">The organization.</param>
        /// <returns>The <see cref="IEnumerable{User}"/> of users that are not <see cref="Organization"/> and are members of <paramref name="organization"/>.</returns>
        public static IEnumerable<User> GetUserAccountMembers(this Organization organization)
        {
            Queue<Organization> organizations = new Queue<Organization>();
            organizations.Enqueue(organization);

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
                        yield return membership.Member;
                    }
                }
            }
        }
    }
}