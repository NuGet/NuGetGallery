// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SupportNewClientHeaders : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageStatistics", "DependentPackage", c => c.String(maxLength: 128));
            AddColumn("dbo.PackageStatistics", "ProjectGuids", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageStatistics", "ProjectGuids");
            DropColumn("dbo.PackageStatistics", "DependentPackage");
        }
    }
}
