using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageReleaseNotes : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "ReleaseNotes", c => c.String(nullable: true));
        }

        public override void Down()
        {
            DropColumn("Packages", "ReleaseNotes");
        }
    }
}