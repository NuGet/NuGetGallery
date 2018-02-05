// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class OrganizationMemberViewModel
    {
        public OrganizationMemberViewModel(Membership membership)
        {
            membership = membership ?? throw new ArgumentNullException(nameof(membership));

            Member = membership.Member;
            Username = membership.Member.Username;
            IsAdmin = membership.IsAdmin;
        }

        public User Member { get; }

        public string Username { get; }

        public bool IsAdmin { get; }
    }
}