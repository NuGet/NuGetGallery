namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRepositoryURL_ReadMe : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "HasReadMe", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "HasReadMe");
        }
    }
}
