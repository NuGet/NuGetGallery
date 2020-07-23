namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddEmbeddedReadmeTypeColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "EmbeddedReadmeType", c => c.Int(nullable: false, defaultValue: 0));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "EmbeddedReadmeType");
        }
    }
}
