namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ApiKeyOwnerScope : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Scopes", "Owner", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Scopes", "Owner");
        }
    }
}
