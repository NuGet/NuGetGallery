// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageLastEditedTimestamp : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "LastEdited", c => c.DateTime());
            CreateIndex("Packages", "LastEdited", name: "IX_Packages_LastEdited");
        }

        public override void Down()
        {
            DropIndex("Packages", "IX_Packages_LastEdited");
            DropColumn("Packages", "LastEdited");
        }
    }
}
