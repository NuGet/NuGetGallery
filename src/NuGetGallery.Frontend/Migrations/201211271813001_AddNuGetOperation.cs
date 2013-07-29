namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddNuGetOperation : DbMigration
    {
        public override void Up()
        {
            AddColumn("PackageStatistics", "Operation", c => c.String(maxLength: 16));
        }
        
        public override void Down()
        {
            DropColumn("PackageStatistics", "Operation");
        }
    }
}
