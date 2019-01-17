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
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        AlternatePackageRegistrationKey = c.Int(),
                        AlternatePackageKey = c.Int(),
                        DeprecatedByKey = c.Int(),
                        DeprecatedOn = c.DateTime(nullable: false),
                        CustomMessage = c.String(),
                        CVSSRating = c.Decimal(precision: 3, scale: 1),
                        CVEIds = c.String(),
                        CWEIds = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.AlternatePackageKey)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.DeprecatedByKey)
                .ForeignKey("dbo.PackageRegistrations", t => t.AlternatePackageRegistrationKey)
                .Index(t => t.PackageKey, unique: true)
                .Index(t => t.AlternatePackageRegistrationKey)
                .Index(t => t.AlternatePackageKey)
                .Index(t => t.DeprecatedByKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.PackageDeprecations", "DeprecatedByKey", "dbo.Users");
            DropForeignKey("dbo.PackageDeprecations", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.PackageDeprecations", "AlternatePackageKey", "dbo.Packages");
            DropIndex("dbo.PackageDeprecations", new[] { "DeprecatedByKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackageKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "AlternatePackageRegistrationKey" });
            DropIndex("dbo.PackageDeprecations", new[] { "PackageKey" });
            DropTable("dbo.PackageDeprecations");
        }
    }
}
