namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Organizations : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Memberships",
                c => new
                    {
                        OrganizationKey = c.Int(nullable: false),
                        MemberKey = c.Int(nullable: false),
                        IsAdmin = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => new { t.OrganizationKey, t.MemberKey })
                .ForeignKey("dbo.Organizations", t => t.OrganizationKey)
                .ForeignKey("dbo.Users", t => t.MemberKey, cascadeDelete: true)
                .Index(t => t.OrganizationKey)
                .Index(t => t.MemberKey);
            
            CreateTable(
                "dbo.Organizations",
                c => new
                    {
                        Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.Key)
                .Index(t => t.Key);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Organizations", "Key", "dbo.Users");
            DropForeignKey("dbo.Memberships", "MemberKey", "dbo.Users");
            DropForeignKey("dbo.Memberships", "OrganizationKey", "dbo.Organizations");
            DropIndex("dbo.Organizations", new[] { "Key" });
            DropIndex("dbo.Memberships", new[] { "MemberKey" });
            DropIndex("dbo.Memberships", new[] { "OrganizationKey" });
            DropTable("dbo.Organizations");
            DropTable("dbo.Memberships");
        }
    }
}
