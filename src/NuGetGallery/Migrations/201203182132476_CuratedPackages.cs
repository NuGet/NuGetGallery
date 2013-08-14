using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class CuratedPackages : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("CuratedPackages", "PackageKey", "Packages");
            DropIndex("CuratedPackages", new[] { "PackageKey" });
            AddColumn("CuratedPackages", "PackageRegistrationKey", c => c.Int(nullable: false));
            AddColumn("CuratedPackages", "AutomaticallyCurated", c => c.Boolean(nullable: false));
            AddColumn("CuratedPackages", "Included", c => c.Boolean(nullable: false));
            AddForeignKey("CuratedPackages", "PackageRegistrationKey", "PackageRegistrations", "Key", cascadeDelete: true);
            CreateIndex("CuratedPackages", "PackageRegistrationKey");
            DropColumn("CuratedPackages", "PackageKey");
        }

        public override void Down()
        {
            AddColumn("CuratedPackages", "PackageKey", c => c.Int(nullable: false));
            DropIndex("CuratedPackages", new[] { "PackageRegistrationKey" });
            DropForeignKey("CuratedPackages", "PackageRegistrationKey", "PackageRegistrations");
            DropColumn("CuratedPackages", "Included");
            DropColumn("CuratedPackages", "AutomaticallyCurated");
            DropColumn("CuratedPackages", "PackageRegistrationKey");
            CreateIndex("CuratedPackages", "PackageKey");
            AddForeignKey("CuratedPackages", "PackageKey", "Packages", "Key", cascadeDelete: true);
        }
    }
}