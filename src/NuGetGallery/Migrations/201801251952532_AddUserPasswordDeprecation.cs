namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserPasswordDeprecation : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "IsPasswordDeprecated", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "IsPasswordDeprecated");
        }
    }
}
