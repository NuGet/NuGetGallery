namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class RemovePackageEdits : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.PackageEdits", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.PackageEdits", "UserKey", "dbo.Users");
            DropIndex("dbo.PackageEdits", new[] { "PackageKey" });
            DropIndex("dbo.PackageEdits", new[] { "UserKey" });
            DropTable("dbo.PackageEdits");
        }
        
        public override void Down()
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
                        LastError = c.String(),
                        ReadMeState = c.String(),
                        Title = c.String(maxLength: 256),
                        Authors = c.String(),
                        Copyright = c.String(),
                        Description = c.String(),
                        IconUrl = c.String(),
                        LicenseUrl = c.String(),
                        ProjectUrl = c.String(),
                        RepositoryUrl = c.String(),
                        ReleaseNotes = c.String(),
                        RequiresLicenseAcceptance = c.Boolean(nullable: false),
                        Summary = c.String(),
                        Tags = c.String(),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateIndex("dbo.PackageEdits", "UserKey");
            CreateIndex("dbo.PackageEdits", "PackageKey");
            AddForeignKey("dbo.PackageEdits", "UserKey", "dbo.Users", "Key");
            AddForeignKey("dbo.PackageEdits", "PackageKey", "dbo.Packages", "Key", cascadeDelete: true);
        }
    }
}
