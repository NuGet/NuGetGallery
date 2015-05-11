// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddNuGetOperation : DbMigration
    {
        public override void Up()
        {
            AddColumn("PackageStatistics", "Operation", c => c.String(maxLength: 16));
        }
        
        public override void Down()
        {
            DropColumn("PackageStatistics", "Operation");
        }
    }
}
