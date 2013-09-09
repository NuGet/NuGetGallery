namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LicenseReports : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageLicenseReports",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        CreatedUtc = c.DateTime(nullable: false),
                        Sequence = c.Int(nullable: false),
                        ReportUrl = c.String(nullable: false, maxLength: 256),
                        Comment = c.String(maxLength: 256),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);
            
            CreateTable(
                "dbo.PackageLicenses",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.PackageLicenseReportLicenses",
                c => new
                    {
                        ReportKey = c.Int(nullable: false),
                        LicenseKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ReportKey, t.LicenseKey })
                .ForeignKey("dbo.PackageLicenseReports", t => t.ReportKey, cascadeDelete: true)
                .ForeignKey("dbo.PackageLicenses", t => t.LicenseKey, cascadeDelete: true)
                .Index(t => t.ReportKey)
                .Index(t => t.LicenseKey);
            
            AddColumn("dbo.Packages", "HideLicenseReport", c => c.Boolean(nullable: false));
            AddColumn("dbo.Packages", "LicenseNames", c => c.String());
            AddColumn("dbo.Packages", "LicenseReportUrl", c => c.String());
            AddColumn("dbo.GallerySettings", "NextLicenseReport", c => c.String());
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageLicenseReportLicenses", new[] { "LicenseKey" });
            DropIndex("dbo.PackageLicenseReportLicenses", new[] { "ReportKey" });
            DropIndex("dbo.PackageLicenseReports", new[] { "PackageKey" });
            DropForeignKey("dbo.PackageLicenseReportLicenses", "LicenseKey", "dbo.PackageLicenses");
            DropForeignKey("dbo.PackageLicenseReportLicenses", "ReportKey", "dbo.PackageLicenseReports");
            DropForeignKey("dbo.PackageLicenseReports", "PackageKey", "dbo.Packages");
            DropColumn("dbo.GallerySettings", "NextLicenseReport");
            DropColumn("dbo.Packages", "LicenseReportUrl");
            DropColumn("dbo.Packages", "LicenseNames");
            DropColumn("dbo.Packages", "HideLicenseReport");
            DropTable("dbo.PackageLicenseReportLicenses");
            DropTable("dbo.PackageLicenses");
            DropTable("dbo.PackageLicenseReports");
        }
    }
}
