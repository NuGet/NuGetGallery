namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DevelopmentDependencyMetadata : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "DevelopmentDependency", c => c.Boolean(nullable: false, defaultValue: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "DevelopmentDependency");
        }
    }
}
