namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCredentialRevokedBy : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "RevokedBy", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "RevokedBy");
        }
    }
}
