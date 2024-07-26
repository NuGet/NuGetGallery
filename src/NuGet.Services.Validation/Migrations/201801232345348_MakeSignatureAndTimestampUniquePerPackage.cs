namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class MakeSignatureAndTimestampUniquePerPackage : DbMigration
    {
        public override void Up()
        {
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey");
            DropIndex("signature.TrustedTimestamps", new[] { "PackageSignatureKey" });
            CreateIndex("signature.PackageSignatures", "PackageKey", unique: true, name: "IX_PackageSignatures_PackageKey");
            CreateIndex("signature.TrustedTimestamps", "PackageSignatureKey", unique: true, name: "IX_TrustedTimestamps_PackageSignatureKey");
        }
        
        public override void Down()
        {
            DropIndex("signature.TrustedTimestamps", "IX_TrustedTimestamps_PackageSignatureKey");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey");
            CreateIndex("signature.TrustedTimestamps", "PackageSignatureKey");
            CreateIndex("signature.PackageSignatures", "PackageKey", name: "IX_PackageSignatures_PackageKey");
        }
    }
}
