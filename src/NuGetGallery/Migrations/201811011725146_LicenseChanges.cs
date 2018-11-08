namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LicenseChanges : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "EmbeddedLicenseType", c => c.Int(nullable: false));
            AddColumn("dbo.Packages", "LicenseExpression", c => c.String(maxLength: 500));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "LicenseExpression");
            DropColumn("dbo.Packages", "EmbeddedLicenseType");
        }
    }
}
