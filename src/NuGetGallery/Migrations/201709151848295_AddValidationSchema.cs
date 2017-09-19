namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddValidationSchema : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageValidationSets",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationTrackingId = c.Guid(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Updated = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.ValidationTrackingId, unique: true)
                .Index(t => t.PackageKey);
            
            CreateTable(
                "dbo.PackageValidations",
                c => new
                    {
                        Key = c.Guid(nullable: false, identity: true),
                        PackageValidationSetKey = c.Long(nullable: false),
                        Type = c.String(nullable: false, maxLength: 255, unicode: false),
                        ValidationStatusKey = c.Int(nullable: false),
                        ValidationStatusTimestamp = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidationSets", t => t.PackageValidationSetKey, cascadeDelete: true)
                .Index(t => t.PackageValidationSetKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageValidations", "PackageValidationSetKey", "dbo.PackageValidationSets");
            DropForeignKey("dbo.PackageValidationSets", "PackageKey", "dbo.Packages");
            DropIndex("dbo.PackageValidations", new[] { "PackageValidationSetKey" });
            DropIndex("dbo.PackageValidationSets", new[] { "PackageKey" });
            DropIndex("dbo.PackageValidationSets", new[] { "ValidationTrackingId" });
            DropTable("dbo.PackageValidations");
            DropTable("dbo.PackageValidationSets");
        }
    }
}
