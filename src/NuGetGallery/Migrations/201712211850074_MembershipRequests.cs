namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MembershipRequests : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.OrganizationMigrationRequests",
                c => new
                    {
                        NewOrganizationKey = c.Int(nullable: false),
                        AdminUserKey = c.Int(nullable: false),
                        ConfirmationToken = c.String(nullable: false),
                        RequestDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.NewOrganizationKey)
                .ForeignKey("dbo.Users", t => t.AdminUserKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.NewOrganizationKey)
                .Index(t => t.NewOrganizationKey)
                .Index(t => t.AdminUserKey);
            
            CreateTable(
                "dbo.MembershipRequests",
                c => new
                    {
                        OrganizationKey = c.Int(nullable: false),
                        NewMemberKey = c.Int(nullable: false),
                        IsAdmin = c.Boolean(nullable: false),
                        ConfirmationToken = c.String(nullable: false),
                        RequestDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => new { t.OrganizationKey, t.NewMemberKey })
                .ForeignKey("dbo.Organizations", t => t.OrganizationKey)
                .ForeignKey("dbo.Users", t => t.NewMemberKey)
                .Index(t => t.OrganizationKey)
                .Index(t => t.NewMemberKey);
            
            AddColumn("dbo.Credentials", "TenantId", c => c.String(maxLength: 256));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.MembershipRequests", "NewMemberKey", "dbo.Users");
            DropForeignKey("dbo.MembershipRequests", "OrganizationKey", "dbo.Organizations");
            DropForeignKey("dbo.OrganizationMigrationRequests", "NewOrganizationKey", "dbo.Users");
            DropForeignKey("dbo.OrganizationMigrationRequests", "AdminUserKey", "dbo.Users");
            DropIndex("dbo.MembershipRequests", new[] { "NewMemberKey" });
            DropIndex("dbo.MembershipRequests", new[] { "OrganizationKey" });
            DropIndex("dbo.OrganizationMigrationRequests", new[] { "AdminUserKey" });
            DropIndex("dbo.OrganizationMigrationRequests", new[] { "NewOrganizationKey" });
            DropColumn("dbo.Credentials", "TenantId");
            DropTable("dbo.MembershipRequests");
            DropTable("dbo.OrganizationMigrationRequests");
        }
    }
}
