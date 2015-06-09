namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DevelopmentDependencyEditable : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageEdits", "DevelopmentDependency", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageEdits", "DevelopmentDependency");
        }
    }
}
