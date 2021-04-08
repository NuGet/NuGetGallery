namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVersionSpecificId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "Id", c => c.String(maxLength: 128));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "Id");
        }
    }
}
