namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackageRowVersion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedPackages", "RowVersion");
        }
    }
}
