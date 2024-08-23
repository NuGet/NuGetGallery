namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageRevalidations : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageRevalidations",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        PackageNormalizedVersion = c.String(nullable: false, maxLength: 64),
                        Enqueued = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidationTrackingId = c.Guid(),
                        Completed = c.Boolean(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => new { t.PackageId, t.PackageNormalizedVersion }, name: "IX_PackageRevalidations_PackageId_PackageNormalizedVersion")
                .Index(t => t.Enqueued, name: "IX_PackageRevalidations_Enqueued")
                .Index(t => t.ValidationTrackingId, unique: true, name: "IX_PackageRevalidations_ValidationTrackingId");
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_ValidationTrackingId");
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_Enqueued");
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_PackageId_PackageNormalizedVersion");
            DropTable("dbo.PackageRevalidations");
        }
    }
}
