namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DevelopmentDependencyEditableInCore : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "DevelopmentDependency", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "DevelopmentDependency");
        }
    }
}
