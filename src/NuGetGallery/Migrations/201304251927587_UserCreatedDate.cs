// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UserCreatedDate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "CreatedUtc", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "CreatedUtc");
        }
    }
}
