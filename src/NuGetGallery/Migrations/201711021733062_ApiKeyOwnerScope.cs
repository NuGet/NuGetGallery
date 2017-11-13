namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ApiKeyOwnerScope : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Scopes", "OwnerKey", c => c.Int());
            CreateIndex("dbo.Scopes", "OwnerKey");
            AddForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users");
            DropIndex("dbo.Scopes", new[] { "OwnerKey" });
            DropColumn("dbo.Scopes", "OwnerKey");
        }
    }
}
