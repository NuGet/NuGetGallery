namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackagesRowVersion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "RowVersion");
        }
    }
}
