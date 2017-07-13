namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PrefixReservation : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ReservedPrefixes",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Pattern = c.String(nullable: false, maxLength: 128),
                        IsPublicNamespace = c.Boolean(nullable: false)
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.PackageRegistrationReservedPrefixes",
                c => new
                    {
                        PackageRegistration_Key = c.Int(nullable: false),
                        ReservedPrefix_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistration_Key, t.ReservedPrefix_Key })
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistration_Key, cascadeDelete: true)
                .ForeignKey("dbo.ReservedPrefixes", t => t.ReservedPrefix_Key, cascadeDelete: true)
                .Index(t => t.PackageRegistration_Key)
                .Index(t => t.ReservedPrefix_Key);
            
            CreateTable(
                "dbo.ReservedPrefixUsers",
                c => new
                    {
                        ReservedPrefix_Key = c.Int(nullable: false),
                        User_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ReservedPrefix_Key, t.User_Key })
                .ForeignKey("dbo.ReservedPrefixes", t => t.ReservedPrefix_Key, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.User_Key, cascadeDelete: true)
                .Index(t => t.ReservedPrefix_Key)
                .Index(t => t.User_Key);
            
            AddColumn("dbo.Users", "Verified", c => c.Boolean(nullable: false));
            AddColumn("dbo.PackageRegistrations", "Verified", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ReservedPrefixUsers", "User_Key", "dbo.Users");
            DropForeignKey("dbo.ReservedPrefixUsers", "ReservedPrefix_Key", "dbo.ReservedPrefixes");
            DropForeignKey("dbo.PackageRegistrationReservedPrefixes", "ReservedPrefix_Key", "dbo.ReservedPrefixes");
            DropForeignKey("dbo.PackageRegistrationReservedPrefixes", "PackageRegistration_Key", "dbo.PackageRegistrations");
            DropIndex("dbo.ReservedPrefixUsers", new[] { "User_Key" });
            DropIndex("dbo.ReservedPrefixUsers", new[] { "ReservedPrefix_Key" });
            DropIndex("dbo.PackageRegistrationReservedPrefixes", new[] { "ReservedPrefix_Key" });
            DropIndex("dbo.PackageRegistrationReservedPrefixes", new[] { "PackageRegistration_Key" });
            DropColumn("dbo.PackageRegistrations", "Verified");
            DropColumn("dbo.Users", "Verified");
            DropTable("dbo.ReservedPrefixUsers");
            DropTable("dbo.PackageRegistrationReservedPrefixes");
            DropTable("dbo.ReservedPrefixes");
        }
    }
}
