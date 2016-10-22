namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CredentialLastUsed : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "LastUsed", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "LastUsed");
        }
    }
}
