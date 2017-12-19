namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TenantId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "TenantId", c => c.String(maxLength: 256));
            AddColumn("dbo.Organizations", "TenantId", c => c.String(maxLength: 256));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Organizations", "TenantId");
            DropColumn("dbo.Credentials", "TenantId");
        }
    }
}
