// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Migrations;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin
{
    public class SupportRequestMigrationsConfiguration
        : DbMigrationsConfiguration<SupportRequestDbContext>
    {
        public SupportRequestMigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = "Areas\\Admin\\Migrations";
            MigrationsNamespace = "NuGetGallery.Areas.Admin";
        }
    }
}