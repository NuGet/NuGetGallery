namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DenormalizeLicenseReportData : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "LicenseNames", c => c.String());
            AddColumn("dbo.Packages", "LicenseReportUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "LicenseReportUrl");
            DropColumn("dbo.Packages", "LicenseNames");
        }
    }
}
