namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EmbeddedIconFlag : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "HasEmbeddedIcon", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "HasEmbeddedIcon");
        }
    }
}
