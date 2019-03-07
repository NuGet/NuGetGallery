namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CvesCanBeEmpty : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Cwes", "Name", c => c.String(nullable: false, maxLength: 200));
            AlterColumn("dbo.Cwes", "Description", c => c.String(nullable: false, maxLength: 300));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Cwes", "Description", c => c.String(maxLength: 300));
            AlterColumn("dbo.Cwes", "Name", c => c.String(maxLength: 200));
        }
    }
}
