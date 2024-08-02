namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageSignaturesTypeStatusIndex : DbMigration
    {
        public override void Up()
        {
            CreateIndex("signature.PackageSignatures", new[] { "Type", "Status" }, name: "IX_PackageSignatures_Type_Status");
        }
        
        public override void Down()
        {
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_Type_Status");
        }
    }
}
