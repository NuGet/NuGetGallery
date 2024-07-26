namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageSignatureType : DbMigration
    {
        public override void Up()
        {
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey");
            AddColumn("signature.PackageSignatures", "Type", c => c.Int(nullable: false, defaultValue: (int)PackageSignatureType.Author));
            CreateIndex("signature.PackageSignatures", new[] { "PackageKey", "Type" }, unique: true, name: "IX_PackageSignatures_PackageKey_Type");
        }
        
        public override void Down()
        {
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey_Type");
            DropColumn("signature.PackageSignatures", "Type");
            CreateIndex("signature.PackageSignatures", "PackageKey", unique: true, name: "IX_PackageSignatures_PackageKey");
        }
    }
}
