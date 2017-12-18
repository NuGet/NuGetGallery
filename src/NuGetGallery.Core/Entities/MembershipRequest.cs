// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class MembershipRequest
    {
        public int OrganizationKey { get; set; }

        public virtual User Organization { get; set; }

        public int NewMemberKey { get; set; }

        public virtual User NewMember { get; set; }

        public bool IsAdmin { get; set; }

        public string ConfirmationCode { get; set; }
    }
}
