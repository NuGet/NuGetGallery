namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NotifyPackagePushed : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "NotifyPackagePushed", c => c.Boolean(nullable: false, defaultValue: true));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "NotifyPackagePushed");
        }
    }
}
