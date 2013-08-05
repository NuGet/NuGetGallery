using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class ChangeCuratedFeedIdToName : DbMigration
    {
        public override void Up()
        {
            RenameColumn("CuratedFeeds", "Id", "Name");
        }

        public override void Down()
        {
            RenameColumn("CuratedFeeds", "Name", "Id");
        }
    }
}