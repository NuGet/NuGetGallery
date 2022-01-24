namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserStatusKeyColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "UserStatusKey", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "UserStatusKey");
        }
    }
}
