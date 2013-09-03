namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class MovingGallerySettingsToConfiguration : DbMigration
    {
        public override void Up()
        {
            DropColumn("GallerySettings", "SmtpHost");
            DropColumn("GallerySettings", "SmtpUsername");
            DropColumn("GallerySettings", "SmtpPassword");
            DropColumn("GallerySettings", "SmtpPort");
            DropColumn("GallerySettings", "UseSmtp");
            DropColumn("GallerySettings", "GalleryOwnerName");
            DropColumn("GallerySettings", "GalleryOwnerEmail");
            DropColumn("GallerySettings", "ConfirmEmailAddresses");
        }
        
        public override void Down()
        {
            AddColumn("GallerySettings", "ConfirmEmailAddresses", c => c.Boolean(nullable: false));
            AddColumn("GallerySettings", "GalleryOwnerEmail", c => c.String());
            AddColumn("GallerySettings", "GalleryOwnerName", c => c.String());
            AddColumn("GallerySettings", "UseSmtp", c => c.Boolean(nullable: false));
            AddColumn("GallerySettings", "SmtpPort", c => c.Int());
            AddColumn("GallerySettings", "SmtpPassword", c => c.String());
            AddColumn("GallerySettings", "SmtpUsername", c => c.String());
            AddColumn("GallerySettings", "SmtpHost", c => c.String());
        }
    }
}
