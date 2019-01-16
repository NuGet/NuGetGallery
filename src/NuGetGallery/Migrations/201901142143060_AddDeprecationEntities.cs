namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDeprecationEntities : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageDeprecations",
                c => new
                    {
                        Key = c.Int(nullable: false),
                        DeprecatedPackageKey = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        AlternatePackageRegistrationKey = c.Int(),
                        AlternatePackageKey = c.Int(),
                        DeprecatedByKey = c.Int(),
                        DeprecatedOn = c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"),
                        CustomMessage = c.String(),
                        PackageVulnerabilityKey = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.AlternatePackageKey)
                .ForeignKey("dbo.PackageRegistrations", t => t.AlternatePackageRegistrationKey)
                .ForeignKey("dbo.Users", t => t.DeprecatedByKey)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .ForeignKey("dbo.PackageVulnerabilities", t => t.PackageVulnerabilityKey)
                .Index(t => t.Key)
                .Index(t => t.AlternatePackageRegistrationKey)
                .Index(t => t.AlternatePackageKey)
                .Index(t => t.DeprecatedByKey)
                .Index(t => t.PackageVulnerabilityKey);
            
            CreateTable(
                "dbo.PackageVulnerabilities",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        CVSSRating = c.Int(),
                        CVEIds = c.String(),
                        CWEIds = c.String(),
                    })
                .PrimaryKey(t => t.Key);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageDeprecations", "PackageVulnerabilityKey", "dbo.PackageVulnerabilities");
            DropForeignKey("dbo.PackageDeprecations", "Key", "dbo.Packages");
            DropForeignKey("dbo.PackageDeprecations", "DeprecatedByKey", "dbo.Users");
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackageKey", "dbo.Packages");
            DropIndex("dbo.PackageDeprecations", new[] { "PackageVulnerabilityKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "DeprecatedByKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackageKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackageRegistrationKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "Key" });
            DropTable("dbo.PackageVulnerabilities");
            DropTable("dbo.PackageDeprecations");
        }
    }
}
