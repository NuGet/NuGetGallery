// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageReleaseNotes : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "ReleaseNotes", c => c.String(nullable: true));
        }

        public override void Down()
        {
            DropColumn("Packages", "ReleaseNotes");
        }
    }
}