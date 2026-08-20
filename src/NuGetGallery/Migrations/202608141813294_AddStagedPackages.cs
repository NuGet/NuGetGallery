namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddStagedPackages : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StagedPackages",
                c => new
                    {
                        PackageKey = c.Int(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        UploadedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.PackageKey)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey)
                .Index(t => t.OwnerKey);
        }

        public override void Down()
        {
            DropForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users");
            DropIndex("dbo.StagedPackages", new[] { "OwnerKey" });
            DropIndex("dbo.StagedPackages", new[] { "PackageKey" });
            DropTable("dbo.StagedPackages");
        }
    }
}
