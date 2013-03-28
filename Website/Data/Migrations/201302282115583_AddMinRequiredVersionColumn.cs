using System.Data.Entity.Migrations;

namespace NuGetGallery.Data.Migrations
{
    public partial class AddMinRequiredVersionColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "MinClientVersion", c => c.String(maxLength: 44));
        }
        
        public override void Down()
        {
            DropColumn("Packages", "MinClientVersion");
        }
    }
}
