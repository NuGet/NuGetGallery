namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSemVerLevelKeyColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "SemVerLevelKey", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "SemVerLevelKey");
        }
    }
}
