// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class SecurityPolicies : DbMigration
    {
        public override void Up()
        {
            CreateTable("UserSecurityPolicies", c => new
            {
                Key = c.Int(nullable: false, identity: true),
                Name = c.String(nullable: false, maxLength: 256),
                UserKey = c.Int(nullable: false),
                Value = c.String(nullable: true, maxLength: 256)
            })
            .PrimaryKey(t => t.Key)
            .ForeignKey("Users", t => t.UserKey);
        }
        
        public override void Down()
        {
            DropTable("UserSecurityPolicies");
        }
    }
}
