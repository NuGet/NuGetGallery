// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddTargetFxToDependencies : DbMigration
    {
        public override void Up()
        {
            AddColumn("PackageDependencies", "TargetFramework", c => c.String());
        }

        public override void Down()
        {
            DropColumn("PackageDependencies", "TargetFramework");
        }
    }
}