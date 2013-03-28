using System.ComponentModel;
using System.Data.Entity.Migrations;

namespace NuGetGallery.Data.Migrations
{
    [Description("Adds the Minimum Required Version Column to the Packages table")]
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
