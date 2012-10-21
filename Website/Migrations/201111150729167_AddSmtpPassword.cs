using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddSmtpPassword : DbMigration
    {
        public override void Up()
        {
            AddColumn("GallerySettings", "SmtpPassword", c => c.String());
        }

        public override void Down()
        {
            DropColumn("GallerySettings", "SmtpPassword");
        }
    }
}