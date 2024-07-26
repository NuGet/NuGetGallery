namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageSigningSchema : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "signature.CertificateChainLinks",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        EndCertificateKey = c.Long(nullable: false),
                        ParentCertificateKey = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey, cascadeDelete: true)
                .ForeignKey("signature.ParentCertificates", t => t.ParentCertificateKey, cascadeDelete: true)
                .Index(t => new { t.EndCertificateKey, t.ParentCertificateKey }, unique: true, name: "IX_CertificateChainLinks_EndCertificateKeyParentCertificateKey");
            
            CreateTable(
                "signature.EndCertificates",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        Thumbprint = c.String(nullable: false, maxLength: 256, unicode: false),
                        Status = c.Int(nullable: false),
                        StatusUpdateTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        NextStatusUpdateTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        LastVerificationTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        RevocationTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidationFailures = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Thumbprint, unique: true, name: "IX_EndCertificates_Thumbprint");
            
            CreateTable(
                "signature.PackageSignatures",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        EndCertificateKey = c.Long(nullable: false),
                        CreatedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Status = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey)
                .ForeignKey("signature.PackageSigningStates", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey, name: "IX_PackageSignatures_PackageKey")
                .Index(t => t.EndCertificateKey, name: "IX_PackageSignatures_EndCertificateKey")
                .Index(t => t.Status, name: "IX_PackageSignatures_Status");
            
            CreateTable(
                "signature.PackageSigningStates",
                c => new
                    {
                        PackageKey = c.Int(nullable: false),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        PackageNormalizedVersion = c.String(nullable: false, maxLength: 64),
                        SigningStatus = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.PackageKey)
                .Index(t => new { t.PackageId, t.PackageNormalizedVersion }, name: "IX_PackageSigningStates_PackageId_PackageNormalizedVersion");
            
            CreateTable(
                "signature.TrustedTimestamps",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageSignatureKey = c.Long(nullable: false),
                        EndCertificateKey = c.Long(nullable: false),
                        Value = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey)
                .ForeignKey("signature.PackageSignatures", t => t.PackageSignatureKey, cascadeDelete: true)
                .Index(t => t.PackageSignatureKey)
                .Index(t => t.EndCertificateKey);
            
            CreateTable(
                "signature.EndCertificateValidations",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        EndCertificateKey = c.Long(nullable: false),
                        ValidationId = c.Guid(nullable: false),
                        Status = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey, cascadeDelete: true)
                .Index(t => new { t.EndCertificateKey, t.ValidationId }, name: "IX_EndCertificateValidations_EndCertificateKey_ValidationId")
                .Index(t => t.ValidationId, name: "IX_EndCertificateValidations_ValidationId");
            
            CreateTable(
                "signature.ParentCertificates",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        Thumbprint = c.String(nullable: false, maxLength: 256, unicode: false),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Thumbprint, unique: true, name: "IX_ParentCertificates_Thumbprint");
            
            CreateTable(
                "dbo.ValidatorStatuses",
                c => new
                    {
                        ValidationId = c.Guid(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        ValidatorName = c.String(nullable: false),
                        State = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.ValidationId)
                .Index(t => t.PackageKey, name: "IX_ValidatorStatuses_PackageKey");
            
        }
        
        public override void Down()
        {
            DropForeignKey("signature.CertificateChainLinks", "ParentCertificateKey", "signature.ParentCertificates");
            DropForeignKey("signature.EndCertificateValidations", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.TrustedTimestamps", "PackageSignatureKey", "signature.PackageSignatures");
            DropForeignKey("signature.TrustedTimestamps", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.PackageSignatures", "PackageKey", "signature.PackageSigningStates");
            DropForeignKey("signature.PackageSignatures", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.CertificateChainLinks", "EndCertificateKey", "signature.EndCertificates");
            DropIndex("dbo.ValidatorStatuses", "IX_ValidatorStatuses_PackageKey");
            DropIndex("signature.ParentCertificates", "IX_ParentCertificates_Thumbprint");
            DropIndex("signature.EndCertificateValidations", "IX_EndCertificateValidations_ValidationId");
            DropIndex("signature.EndCertificateValidations", "IX_EndCertificateValidations_EndCertificateKey_ValidationId");
            DropIndex("signature.TrustedTimestamps", new[] { "EndCertificateKey" });
            DropIndex("signature.TrustedTimestamps", new[] { "PackageSignatureKey" });
            DropIndex("signature.PackageSigningStates", "IX_PackageSigningStates_PackageId_PackageNormalizedVersion");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_Status");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_EndCertificateKey");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey");
            DropIndex("signature.EndCertificates", "IX_EndCertificates_Thumbprint");
            DropIndex("signature.CertificateChainLinks", "IX_CertificateChainLinks_EndCertificateKeyParentCertificateKey");
            DropTable("dbo.ValidatorStatuses");
            DropTable("signature.ParentCertificates");
            DropTable("signature.EndCertificateValidations");
            DropTable("signature.TrustedTimestamps");
            DropTable("signature.PackageSigningStates");
            DropTable("signature.PackageSignatures");
            DropTable("signature.EndCertificates");
            DropTable("signature.CertificateChainLinks");
        }
    }
}
