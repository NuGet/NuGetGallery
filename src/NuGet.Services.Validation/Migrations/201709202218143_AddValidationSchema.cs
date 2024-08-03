namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddValidationSchema : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageValidations",
                c => new
                    {
                        Key = c.Guid(nullable: false, identity: true),
                        PackageValidationSetKey = c.Long(nullable: false),
                        Type = c.String(nullable: false, maxLength: 255, unicode: false),
                        Started = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidationStatus = c.Int(nullable: false),
                        ValidationStatusTimestamp = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidationSets", t => t.PackageValidationSetKey, cascadeDelete: true)
                .Index(t => t.PackageValidationSetKey);
            
            CreateTable(
                "dbo.PackageValidationSets",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationTrackingId = c.Guid(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        PackageNormalizedVersion = c.String(nullable: false, maxLength: 64),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Updated = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.ValidationTrackingId, unique: true, name: "IX_PackageValidationSets_ValidationTrackingId")
                .Index(t => t.PackageKey, name: "IX_PackageValidationSets_PackageKey")
                .Index(t => new { t.PackageId, t.PackageNormalizedVersion }, name: "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageValidations", "PackageValidationSetKey", "dbo.PackageValidationSets");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageKey");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_ValidationTrackingId");
            DropIndex("dbo.PackageValidations", new[] { "PackageValidationSetKey" });
            DropTable("dbo.PackageValidationSets");
            DropTable("dbo.PackageValidations");
        }
    }
}
