using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class RemoveSqlDownloadStatsAggregation : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.GallerySettings", "DownloadStatsLastAggregatedId");
            DropColumn("dbo.GallerySettings", "TotalDownloadCount");
        }

        public override void Down()
        {
            AddColumn("dbo.GallerySettings", "TotalDownloadCount", c => c.Long());
            AddColumn("dbo.GallerySettings", "DownloadStatsLastAggregatedId", c => c.Int());
        }
    }
}
