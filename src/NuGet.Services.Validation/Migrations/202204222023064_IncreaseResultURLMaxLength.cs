namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class IncreaseResultURLMaxLength : DbMigration
    {
        public override void Up()
        {
            AlterColumn("scan.ScanOperationStates", "ResultUrl", c => c.String(maxLength: 2048));
        }
        
        public override void Down()
        {
            AlterColumn("scan.ScanOperationStates", "ResultUrl", c => c.String(maxLength: 512));
        }
    }
}
