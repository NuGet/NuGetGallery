namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class LanguageProperty : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "Language", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("Packages", "Language");
        }
    }
}
