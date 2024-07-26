namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class ScanBlobSize : DbMigration
    {
        public override void Up()
        {
            AddColumn("scan.ScanOperationStates", "BlobSize", c => c.Long());
        }
        
        public override void Down()
        {
            DropColumn("scan.ScanOperationStates", "BlobSize");
        }
    }
}
