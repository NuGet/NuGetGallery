// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class OrganizationExtensions
    {
        public static Membership GetMembershipOfUser(this Organization organization, User member)
        {
            return organization?.Members?.FirstOrDefault(m => m.Member.MatchesUser(member));
        }
    }
}