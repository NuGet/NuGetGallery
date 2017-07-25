namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PrefixReservation : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ReservedNamespaces",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Value = c.String(nullable: false, maxLength: 128),
                        IsSharedNamespace = c.Boolean(nullable: false),
                        IsPrefix = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.ReservedNamespaceOwners",
                c => new
                    {
                        ReservedNamespaceKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ReservedNamespaceKey, t.UserKey })
                .ForeignKey("dbo.ReservedNamespaces", t => t.ReservedNamespaceKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.ReservedNamespaceKey)
                .Index(t => t.UserKey);
            
            CreateTable(
                "dbo.ReservedNamespaceRegistrations",
                c => new
                    {
                        ReservedNamespaceKey = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ReservedNamespaceKey, t.PackageRegistrationKey })
                .ForeignKey("dbo.ReservedNamespaces", t => t.ReservedNamespaceKey, cascadeDelete: true)
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .Index(t => t.ReservedNamespaceKey)
                .Index(t => t.PackageRegistrationKey);
            
            AddColumn("dbo.PackageRegistrations", "IsVerified", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ReservedNamespaceRegistrations", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.ReservedNamespaceRegistrations", "ReservedNamespaceKey", "dbo.ReservedNamespaces");
            DropForeignKey("dbo.ReservedNamespaceOwners", "UserKey", "dbo.Users");
            DropForeignKey("dbo.ReservedNamespaceOwners", "ReservedNamespaceKey", "dbo.ReservedNamespaces");
            DropIndex("dbo.ReservedNamespaceRegistrations", new[] { "PackageRegistrationKey" });
            DropIndex("dbo.ReservedNamespaceRegistrations", new[] { "ReservedNamespaceKey" });
            DropIndex("dbo.ReservedNamespaceOwners", new[] { "UserKey" });
            DropIndex("dbo.ReservedNamespaceOwners", new[] { "ReservedNamespaceKey" });
            DropColumn("dbo.PackageRegistrations", "IsVerified");
            DropTable("dbo.ReservedNamespaceRegistrations");
            DropTable("dbo.ReservedNamespaceOwners");
            DropTable("dbo.ReservedNamespaces");
        }
    }
}
