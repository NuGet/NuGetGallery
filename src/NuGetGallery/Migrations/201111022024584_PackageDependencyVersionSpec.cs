// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageDependencyVersionSpec : DbMigration
    {
        public override void Up()
        {
            AlterColumn("PackageDependencies", "VersionRange", col => col.String(nullable: true));
            RenameColumn("PackageDependencies", "VersionRange", "VersionSpec");
        }

        public override void Down()
        {
            RenameColumn("PackageDependencies", "VersionSpec", "VersionRange");
        }
    }
}