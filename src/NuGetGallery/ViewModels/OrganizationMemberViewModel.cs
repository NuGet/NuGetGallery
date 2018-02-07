﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class OrganizationMemberViewModel
    {
        public OrganizationMemberViewModel(Membership membership)
        {
            var member = membership?.Member ?? throw new ArgumentNullException(nameof(membership));

            Username = member.Username;
            EmailAddress = member.EmailAddress;
            IsAdmin = membership.IsAdmin;
            GravatarUrl = GravatarHelper.Url(EmailAddress, Constants.GravatarElementSize);
        }

        public string Username { get; }

        public string EmailAddress { get; }

        public bool IsAdmin { get; }

        public string GravatarUrl { get; }
    }
}