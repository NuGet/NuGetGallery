// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class MembershipRequest
    {
        public int OrganizationKey { get; set; }

        public virtual Organization Organization { get; set; }

        public int NewMemberKey { get; set; }

        public virtual User NewMember { get; set; }

        public bool IsAdmin { get; set; }

        [Required]
        public string ConfirmationToken { get; set; }

        public DateTime RequestDate { get; set; }
    }
}
