// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class WidenStatisticsOperationColumn : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 18));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 16));
        }
    }
}
