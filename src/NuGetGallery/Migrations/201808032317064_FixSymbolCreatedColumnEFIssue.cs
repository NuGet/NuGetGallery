namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixSymbolCreatedColumnEFIssue : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.SymbolPackages", "Created", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.SymbolPackages", "Created", c => c.DateTime(nullable: false));
        }
    }
}
