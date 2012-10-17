namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class GallerySetting_TotalDownloadCount : DbMigration
    {
        public override void Up()
        {
            AddColumn("GallerySettings", "TotalDownloadCount", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("GallerySettings", "TotalDownloadCount");
        }
    }
}
