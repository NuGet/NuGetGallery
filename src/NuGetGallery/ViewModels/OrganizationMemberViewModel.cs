// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class OrganizationMemberViewModel
    {
        public OrganizationMemberViewModel(Membership membership, string gravatarUrl)
            : this(membership?.Member, gravatarUrl)
        {
            IsAdmin = membership.IsAdmin;
            Pending = false;
        }

        public OrganizationMemberViewModel(MembershipRequest request, string gravatarUrl)
            : this(request?.NewMember, gravatarUrl)
        {
            IsAdmin = request.IsAdmin;
            Pending = true;
        }

        private OrganizationMemberViewModel(User member, string gravatarUrl)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            if (string.IsNullOrEmpty(gravatarUrl))
            {
                throw new ArgumentNullException(nameof(gravatarUrl));
            }

            Username = member.Username;
            EmailAddress = member.EmailAddress;
            GravatarUrl = gravatarUrl;
        }

        public string Username { get; }

        public bool IsAdmin { get; }

        public bool Pending { get; }

        public string EmailAddress { get; }

        public string GravatarUrl { get; }
    }
}