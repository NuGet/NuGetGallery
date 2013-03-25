using System.Data.Entity.Migrations;

namespace NuGetGallery.Data.Migrations
{
    public partial class GallerySettings_TotalDownloadCount : DbMigration
    {
        public override void Up()
        {
            AddColumn("GallerySettings", "TotalDownloadCount", c => c.Long());
        }
        
        public override void Down()
        {
            DropColumn("GallerySettings", "TotalDownloadCount");
        }
    }
}
