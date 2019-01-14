// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class OrganizationMigrationRequest
    {
        public int NewOrganizationKey { get; set; }

        public virtual User NewOrganization { get; set; }

        public int AdminUserKey { get; set; }

        public virtual User AdminUser { get; set; }

        [Required]
        public string ConfirmationToken { get; set; }

        public DateTime RequestDate { get; set; }
    }
}
