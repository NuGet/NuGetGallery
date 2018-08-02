﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class OrganizationMemberViewModel
    {
        public OrganizationMemberViewModel(Membership membership)
            : this(membership?.Member)
        {
            IsAdmin = membership.IsAdmin;
            Pending = false;
        }

        public OrganizationMemberViewModel(MembershipRequest request)
            : this(request?.NewMember)
        {
            IsAdmin = request.IsAdmin;
            Pending = true;
        }

        private OrganizationMemberViewModel(User member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            Username = member.Username;
            EmailAddress = member.EmailAddress;
            GravatarUrl = GravatarHelper.Url(EmailAddress, Constants.GravatarElementSize);
        }

        public string Username { get; }

        public bool IsAdmin { get; }

        public bool Pending { get; }

        public string EmailAddress { get; }

        public string GravatarUrl { get; }
    }
}