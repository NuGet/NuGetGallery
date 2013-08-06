namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EditableMetadata : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageEdits",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        TriedCount = c.Int(nullable: false),
                        Authors = c.String(),
                        Copyright = c.String(),
                        Description = c.String(),
                        IconUrl = c.String(),
                        LicenseUrl = c.String(),
                        ProjectUrl = c.String(),
                        ReleaseNotes = c.String(),
                        RequiresLicenseAcceptance = c.Boolean(nullable: false),
                        Summary = c.String(),
                        Tags = c.String(),
                        Title = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.PackageKey)
                .Index(t => t.UserKey);
            
            CreateTable(
                "dbo.PackageHistories",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        UserKey = c.Int(),
                        Timestamp = c.DateTime(nullable: false),
                        Authors = c.String(),
                        Copyright = c.String(),
                        Description = c.String(),
                        Hash = c.String(),
                        HashAlgorithm = c.String(maxLength: 10),
                        IconUrl = c.String(),
                        LicenseUrl = c.String(),
                        PackageFileSize = c.Long(nullable: false),
                        ProjectUrl = c.String(),
                        ReleaseNotes = c.String(),
                        RequiresLicenseAcceptance = c.Boolean(nullable: false),
                        Summary = c.String(),
                        Tags = c.String(),
                        Title = c.String(),
                        LastUpdated = c.DateTime(nullable: false),
                        Published = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey)
                .Index(t => t.PackageKey)
                .Index(t => t.UserKey);
            
            AddColumn("dbo.Packages", "UserKey", c => c.Int());
            AddForeignKey("dbo.Packages", "UserKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageHistories", new[] { "UserKey" });
            DropIndex("dbo.PackageHistories", new[] { "PackageKey" });
            DropIndex("dbo.PackageEdits", new[] { "UserKey" });
            DropIndex("dbo.PackageEdits", new[] { "PackageKey" });
            DropForeignKey("dbo.PackageHistories", "UserKey", "dbo.Users");
            DropForeignKey("dbo.PackageHistories", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.PackageEdits", "UserKey", "dbo.Users");
            DropForeignKey("dbo.PackageEdits", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.Packages", "UserKey", "dbo.Users");
            DropColumn("dbo.Packages", "UserKey");
            DropTable("dbo.PackageHistories");
            DropTable("dbo.PackageEdits");
        }
    }
}
