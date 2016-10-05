namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserFailedLogin : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "LastFailedLoginUtc", c => c.DateTime());
            AddColumn("dbo.Users", "FailedLoginCount", c => c.Int(nullable: false, identity: false, defaultValue: 0));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "FailedLoginCount");
            DropColumn("dbo.Users", "LastFailedLoginUtc");
        }
    }
}
