namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MembershipRequests : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.MembershipRequests",
                c => new
                    {
                        OrganizationKey = c.Int(nullable: false),
                        NewMemberKey = c.Int(nullable: false),
                        IsAdmin = c.Boolean(nullable: false),
                        ConfirmationCode = c.String(),
                    })
                .PrimaryKey(t => new { t.OrganizationKey, t.NewMemberKey })
                .ForeignKey("dbo.Users", t => t.OrganizationKey)
                .ForeignKey("dbo.Users", t => t.NewMemberKey)
                .Index(t => t.OrganizationKey)
                .Index(t => t.NewMemberKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.MembershipRequests", "NewMemberKey", "dbo.Users");
            DropForeignKey("dbo.MembershipRequests", "OrganizationKey", "dbo.Users");
            DropIndex("dbo.MembershipRequests", new[] { "NewMemberKey" });
            DropIndex("dbo.MembershipRequests", new[] { "OrganizationKey" });
            DropTable("dbo.MembershipRequests");
        }
    }
}
