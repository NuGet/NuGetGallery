namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EditableMetadata : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageEdits",
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
                .ForeignKey("Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("Users", t => t.UserKey)
                .Index(t => t.PackageKey)
                .Index(t => t.UserKey);
            
            CreateTable(
                "PackageHistories",
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
                .ForeignKey("Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);

            // SQL script: redefines the UserKey foreign key with 'ON DELETE SET NULL'
            Sql(@"
ALTER TABLE [PackageHistories] WITH CHECK ADD CONSTRAINT [FK_PackageHistories_Users_UserKey] FOREIGN KEY([UserKey])
REFERENCES [Users] ([Key]) ON DELETE SET NULL");
            CreateIndex("PackageHistories", "UserKey");

            AddColumn("Packages", "UserKey", c => c.Int());
            AddForeignKey("Packages", "UserKey", "Users", "Key");
            CreateIndex("Packages", "UserKey");
        }
        
        public override void Down()
        {
            DropIndex("PackageHistories", new[] { "UserKey" });
            DropIndex("PackageHistories", new[] { "PackageKey" });
            DropIndex("PackageEdits", new[] { "UserKey" });
            DropIndex("PackageEdits", new[] { "PackageKey" });
            DropIndex("Packages", new[] { "UserKey" });
            DropForeignKey("PackageHistories", "UserKey", "Users");
            DropForeignKey("PackageHistories", "PackageKey", "Packages");
            DropForeignKey("PackageEdits", "UserKey", "Users");
            DropForeignKey("PackageEdits", "PackageKey", "Packages");
            DropForeignKey("Packages", "UserKey", "Users");
            DropColumn("Packages", "UserKey");
            DropTable("PackageHistories");
            DropTable("PackageEdits");
        }
    }
}
