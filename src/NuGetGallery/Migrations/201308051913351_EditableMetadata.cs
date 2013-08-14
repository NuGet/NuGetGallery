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
                        Title = c.String(maxLength: 256),
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
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey)
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
                        Title = c.String(maxLength: 256),
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
                        Hash = c.String(maxLength: 256),
                        HashAlgorithm = c.String(maxLength: 10),
                        PackageFileSize = c.Long(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                        Published = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey)
                .Index(t => t.PackageKey)
                .Index(t => t.UserKey);
            
            AddColumn("dbo.Packages", "UserKey", c => c.Int());

            // SQL to create the Foreign Key UserKey with extra ON DELETE SET NULL
            Sql(@"
ALTER TABLE [dbo].[PackageHistories] WITH CHECK ADD CONSTRAINT [FK_dbo.PackageHistories_dbo.Users_UserKey] FOREIGN KEY([UserKey])
REFERENCES [dbo].[Users] ([Key]) ON DELETE SET NULL");
            CreateIndex("dbo.Packages", "UserKey");
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageHistories", new[] { "UserKey" });
            DropIndex("dbo.PackageHistories", new[] { "PackageKey" });
            DropIndex("dbo.PackageEdits", new[] { "UserKey" });
            DropIndex("dbo.PackageEdits", new[] { "PackageKey" });
            DropIndex("dbo.Packages", new[] { "UserKey" });
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
