namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageEditLastErrorColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("PackageEdits", "LastError", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("PackageEdits", "LastError");
        }
    }
}
