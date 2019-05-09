namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IconFlatContainerFlag : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "UsesIconFromFlatContainer", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "UsesIconFromFlatContainer");
        }
    }
}
