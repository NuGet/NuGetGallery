using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class GalleryOwnerEmailSettings : DbMigration
    {
        public override void Up()
        {
            AddColumn("GallerySettings", "UseSmtp", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("GallerySettings", "GalleryOwnerName", c => c.String());
            AddColumn("GallerySettings", "GalleryOwnerEmail", c => c.String());
            AddColumn("GallerySettings", "ConfirmEmailAddresses", c => c.Boolean(nullable: false, defaultValue: true));
        }

        public override void Down()
        {
            DropColumn("GallerySettings", "ConfirmEmailAddresses");
            DropColumn("GallerySettings", "GalleryOwnerEmail");
            DropColumn("GallerySettings", "GalleryOwnerName");
            DropColumn("GallerySettings", "UseSmtp");
        }
    }
}