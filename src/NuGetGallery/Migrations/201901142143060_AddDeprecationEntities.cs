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
                        AlternatePackage_Key = c.Int(),
                        AlternatePackageRegistration_Key = c.Int(),
                        DeprecatedBy_Key = c.Int(),
                        PackageVulnerability_Key = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.AlternatePackage_Key)
                .ForeignKey("dbo.PackageRegistrations", t => t.AlternatePackageRegistration_Key)
                .ForeignKey("dbo.Users", t => t.DeprecatedBy_Key)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .ForeignKey("dbo.PackageVulnerabilities", t => t.PackageVulnerability_Key)
                .Index(t => t.Key)
                .Index(t => t.AlternatePackage_Key)
                .Index(t => t.AlternatePackageRegistration_Key)
                .Index(t => t.DeprecatedBy_Key)
                .Index(t => t.PackageVulnerability_Key);
            
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
            DropForeignKey("dbo.PackageDeprecations", "PackageVulnerability_Key", "dbo.PackageVulnerabilities");
            DropForeignKey("dbo.PackageDeprecations", "Key", "dbo.Packages");
            DropForeignKey("dbo.PackageDeprecations", "DeprecatedBy_Key", "dbo.Users");
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackageRegistration_Key", "dbo.PackageRegistrations");
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackage_Key", "dbo.Packages");
            DropIndex("dbo.PackageDeprecations", new[] { "PackageVulnerability_Key" });
            DropIndex("dbo.PackageDeprecations", new[] { "DeprecatedBy_Key" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackageRegistration_Key" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackage_Key" });
            DropIndex("dbo.PackageDeprecations", new[] { "Key" });
            DropTable("dbo.PackageVulnerabilities");
            DropTable("dbo.PackageDeprecations");
        }
    }
}
