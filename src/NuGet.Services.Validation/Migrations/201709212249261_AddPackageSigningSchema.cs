namespace NuGet.Services.Validation
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageSigningSchema : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "signature.Certificates",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        Thumbprint = c.String(nullable: false, maxLength: 20, fixedLength: true, unicode: false),
                        Status = c.Int(nullable: false),
                        StatusUpdateTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        NextStatusUpdateTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        LastVerificationTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        RevocationTime = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidationFailures = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Thumbprint, unique: true, name: "IX_Certificates_Thumbprint");
            
            CreateTable(
                "signature.PackageSignatures",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        SignedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        CreatedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Status = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.PackageSigningStates", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey, name: "IX_PackageSignatures_PackageKey")
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
                "signature.CertificateValidations",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        CertificateKey = c.Long(nullable: false),
                        ValidationId = c.Guid(nullable: false),
                        Status = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.Certificates", t => t.CertificateKey, cascadeDelete: true)
                .Index(t => new { t.CertificateKey, t.ValidationId }, name: "IX_CertificateValidations_CertificateKey_ValidationId")
                .Index(t => t.ValidationId, name: "IX_CertificateValidations_ValidationId");
            
            CreateTable(
                "dbo.ValidatorStatuses",
                c => new
                    {
                        ValidationId = c.Guid(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        State = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ValidationId)
                .Index(t => t.PackageKey, name: "IX_ValidatorStatuses_PackageKey");
            
            CreateTable(
                "signature.PackageSignatureCertificates",
                c => new
                    {
                        PackageSignatureKey = c.Long(nullable: false),
                        CertificateKey = c.Long(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageSignatureKey, t.CertificateKey })
                .ForeignKey("signature.PackageSignatures", t => t.PackageSignatureKey, cascadeDelete: true)
                .ForeignKey("signature.Certificates", t => t.CertificateKey, cascadeDelete: true)
                .Index(t => t.PackageSignatureKey)
                .Index(t => t.CertificateKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("signature.CertificateValidations", "CertificateKey", "signature.Certificates");
            DropForeignKey("signature.PackageSignatures", "PackageKey", "signature.PackageSigningStates");
            DropForeignKey("signature.PackageSignatureCertificates", "CertificateKey", "signature.Certificates");
            DropForeignKey("signature.PackageSignatureCertificates", "PackageSignatureKey", "signature.PackageSignatures");
            DropIndex("signature.PackageSignatureCertificates", new[] { "CertificateKey" });
            DropIndex("signature.PackageSignatureCertificates", new[] { "PackageSignatureKey" });
            DropIndex("dbo.ValidatorStatuses", "IX_ValidatorStatuses_PackageKey");
            DropIndex("signature.CertificateValidations", "IX_CertificateValidations_ValidationId");
            DropIndex("signature.CertificateValidations", "IX_CertificateValidations_CertificateKey_ValidationId");
            DropIndex("signature.PackageSigningStates", "IX_PackageSigningStates_PackageId_PackageNormalizedVersion");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_Status");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey");
            DropIndex("signature.Certificates", "IX_Certificates_Thumbprint");
            DropTable("signature.PackageSignatureCertificates");
            DropTable("dbo.ValidatorStatuses");
            DropTable("signature.CertificateValidations");
            DropTable("signature.PackageSigningStates");
            DropTable("signature.PackageSignatures");
            DropTable("signature.Certificates");
        }
    }
}
