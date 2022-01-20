namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddLockedUser : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "IsLocked", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "IsLocked");
        }
    }
}
