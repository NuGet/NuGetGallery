namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageVulnerabilities : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.VulnerablePackageVersionRanges",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        VulnerabilityKey = c.Int(nullable: false),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        PackageVersionRange = c.String(nullable: false, maxLength: 132),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageVulnerabilities", t => t.VulnerabilityKey, cascadeDelete: true)
                .Index(t => new { t.VulnerabilityKey, t.PackageId, t.PackageVersionRange }, unique: true);
            
            CreateTable(
                "dbo.PackageVulnerabilities",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        GitHubDatabaseKey = c.Int(nullable: false),
                        ReferenceUrl = c.String(),
                        Severity = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.GitHubDatabaseKey, unique: true);
            
            CreateTable(
                "dbo.VulnerablePackageVersionRangePackages",
                c => new
                    {
                        VulnerablePackageVersionRange_Key = c.Int(nullable: false),
                        Package_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.VulnerablePackageVersionRange_Key, t.Package_Key })
                .ForeignKey("dbo.VulnerablePackageVersionRanges", t => t.VulnerablePackageVersionRange_Key, cascadeDelete: true)
                .ForeignKey("dbo.Packages", t => t.Package_Key, cascadeDelete: true)
                .Index(t => t.VulnerablePackageVersionRange_Key)
                .Index(t => t.Package_Key);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VulnerablePackageVersionRanges", "VulnerabilityKey", "dbo.PackageVulnerabilities");
            DropForeignKey("dbo.VulnerablePackageVersionRangePackages", "Package_Key", "dbo.Packages");
            DropForeignKey("dbo.VulnerablePackageVersionRangePackages", "VulnerablePackageVersionRange_Key", "dbo.VulnerablePackageVersionRanges");
            DropIndex("dbo.VulnerablePackageVersionRangePackages", new[] { "Package_Key" });
            DropIndex("dbo.VulnerablePackageVersionRangePackages", new[] { "VulnerablePackageVersionRange_Key" });
            DropIndex("dbo.PackageVulnerabilities", new[] { "GitHubDatabaseKey" });
            DropIndex("dbo.VulnerablePackageVersionRanges", new[] { "VulnerabilityKey", "PackageId", "PackageVersionRange" });
            DropTable("dbo.VulnerablePackageVersionRangePackages");
            DropTable("dbo.PackageVulnerabilities");
            DropTable("dbo.VulnerablePackageVersionRanges");
        }
    }
}
