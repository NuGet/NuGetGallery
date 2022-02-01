namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCreatedSecurelyToCredentials : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "WasCreatedSecurely", c => c.Boolean());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "WasCreatedSecurely");
        }
    }
}
