namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
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
