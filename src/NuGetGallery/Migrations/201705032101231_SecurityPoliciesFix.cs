namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SecurityPoliciesFix : DbMigration
    {
        public override void Up()
        {
            Sql("IF OBJECT_ID('dbo.UserSecurityPolicies', 'U') IS NOT NULL DROP TABLE [dbo].[UserSecurityPolicies]");

            CreateTable(
                "dbo.UserSecurityPolicies",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 256),
                        Value = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserSecurityPolicies", "UserKey", "dbo.Users");
            DropIndex("dbo.UserSecurityPolicies", new[] { "UserKey" });
            DropTable("dbo.UserSecurityPolicies");

            // revert to previous migration.
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
    }
}
