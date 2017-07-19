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
                "dbo.PackageRegistrationReservedNamespaces",
                c => new
                    {
                        PackageRegistration_Key = c.Int(nullable: false),
                        ReservedNamespace_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistration_Key, t.ReservedNamespace_Key })
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistration_Key, cascadeDelete: true)
                .ForeignKey("dbo.ReservedNamespaces", t => t.ReservedNamespace_Key, cascadeDelete: true)
                .Index(t => t.PackageRegistration_Key)
                .Index(t => t.ReservedNamespace_Key);
            
            CreateTable(
                "dbo.ReservedNamespaceUsers",
                c => new
                    {
                        ReservedNamespace_Key = c.Int(nullable: false),
                        User_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ReservedNamespace_Key, t.User_Key })
                .ForeignKey("dbo.ReservedNamespaces", t => t.ReservedNamespace_Key, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.User_Key, cascadeDelete: true)
                .Index(t => t.ReservedNamespace_Key)
                .Index(t => t.User_Key);
            
            AddColumn("dbo.PackageRegistrations", "IsVerified", c => c.Boolean(nullable: false));
        }

        public override void Down()
        {
            DropForeignKey("dbo.ReservedNamespaceUsers", "User_Key", "dbo.Users");
            DropForeignKey("dbo.ReservedNamespaceUsers", "ReservedNamespace_Key", "dbo.ReservedNamespaces");
            DropForeignKey("dbo.PackageRegistrationReservedNamespaces", "ReservedNamespace_Key", "dbo.ReservedNamespaces");
            DropForeignKey("dbo.PackageRegistrationReservedNamespaces", "PackageRegistration_Key", "dbo.PackageRegistrations");
            DropIndex("dbo.ReservedNamespaceUsers", new[] { "User_Key" });
            DropIndex("dbo.ReservedNamespaceUsers", new[] { "ReservedNamespace_Key" });
            DropIndex("dbo.PackageRegistrationReservedNamespaces", new[] { "ReservedNamespace_Key" });
            DropIndex("dbo.PackageRegistrationReservedNamespaces", new[] { "PackageRegistration_Key" });
            DropColumn("dbo.PackageRegistrations", "IsVerified");
            DropTable("dbo.ReservedNamespaceUsers");
            DropTable("dbo.PackageRegistrationReservedNamespaces");
            DropTable("dbo.ReservedNamespaces");
        }
    }
}
