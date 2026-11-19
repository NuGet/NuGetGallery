namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Adds publication-origin markers for packages and symbol packages.
    /// </summary>
    public partial class AddPublishedFromStaging : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "PublishedFromStaging", c => c.Boolean(nullable: false));
            AddColumn("dbo.SymbolPackages", "PublishedFromStaging", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.SymbolPackages", "PublishedFromStaging");
            DropColumn("dbo.Packages", "PublishedFromStaging");
        }
    }
}
