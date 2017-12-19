namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TenantId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "TenantId", c => c.String(maxLength: 256));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "TenantId");
        }
    }
}
