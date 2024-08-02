namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddErrorCodeOperationDetails : DbMigration
    {
        public override void Up()
        {
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_PackageValidationKey_AttemptIndex");
            AddColumn("scan.ScanOperationStates", "ErrorCode", c => c.String());
            AddColumn("scan.ScanOperationStates", "OperationDetails", c => c.String());
            CreateIndex("scan.ScanOperationStates", new[] { "PackageValidationKey", "OperationType", "AttemptIndex" }, unique: true, name: "IX_ScanOperationStates_PackageValidationKey_OperationType_AttemptIndex");
        }
        
        public override void Down()
        {
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_PackageValidationKey_OperationType_AttemptIndex");
            DropColumn("scan.ScanOperationStates", "OperationDetails");
            DropColumn("scan.ScanOperationStates", "ErrorCode");
            CreateIndex("scan.ScanOperationStates", new[] { "PackageValidationKey", "AttemptIndex" }, unique: true, name: "IX_ScanOperationStates_PackageValidationKey_AttemptIndex");
        }
    }
}
