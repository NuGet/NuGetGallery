namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Language : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "Language", c => c.String(maxLength: 20));
        }
        
        public override void Down()
        {
            DropColumn("Packages", "Language");
        }
    }
}
