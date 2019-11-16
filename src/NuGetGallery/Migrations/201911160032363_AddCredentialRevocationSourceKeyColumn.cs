namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCredentialRevocationSourceKeyColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "RevocationSourceKey", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "RevocationSourceKey");
        }
    }
}
