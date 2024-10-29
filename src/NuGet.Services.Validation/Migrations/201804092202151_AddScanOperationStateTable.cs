namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddScanOperationStateTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "scan.ScanOperationStates",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageValidationKey = c.Guid(nullable: false),
                        OperationType = c.Int(nullable: false),
                        ScanState = c.Int(nullable: false),
                        AttemptIndex = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        StartedAt = c.DateTime(precision: 7, storeType: "datetime2"),
                        FinishedAt = c.DateTime(precision: 7, storeType: "datetime2"),
                        ResultUrl = c.String(maxLength: 512),
                        OperationId = c.String(maxLength: 64),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => new { t.PackageValidationKey, t.AttemptIndex }, unique: true, name: "IX_ScanOperationStates_PackageValidationKey_AttemptIndex")
                .Index(t => new { t.ScanState, t.CreatedAt }, name: "IX_ScanOperationStates_ScanState_Created");
            
        }
        
        public override void Down()
        {
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_ScanState_Created");
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_PackageValidationKey_AttemptIndex");
            DropTable("scan.ScanOperationStates");
        }
    }
}
