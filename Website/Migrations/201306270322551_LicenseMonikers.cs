namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LicenseMonikers : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "LicensesNames", c => c.String());
            AddColumn("dbo.Packages", "SonatypeReportUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "SonatypeReportUrl");
            DropColumn("dbo.Packages", "LicensesNames");
        }
    }
}
