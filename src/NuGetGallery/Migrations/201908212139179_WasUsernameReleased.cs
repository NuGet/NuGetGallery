namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class WasUsernameReleased : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AccountDeletes", "WasUsernameReleased", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.AccountDeletes", "WasUsernameReleased");
        }
    }
}
