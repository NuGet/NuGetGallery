namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddTargetFxToDependencies : DbMigration
    {
        public override void Up()
        {
            AddColumn("PackageDependencies", "TargetFramework", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("PackageDependencies", "TargetFramework");
        }
    }
}
