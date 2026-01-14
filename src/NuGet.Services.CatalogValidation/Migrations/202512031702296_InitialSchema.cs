namespace NuGet.Services.CatalogValidation
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialSchema : DbMigration
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
                        Use = c.Int(nullable: false),
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
                        Type = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey)
                .ForeignKey("signature.PackageSigningStates", t => t.PackageKey, cascadeDelete: true)
                .Index(t => new { t.PackageKey, t.Type }, unique: true, name: "IX_PackageSignatures_PackageKey_Type")
                .Index(t => t.EndCertificateKey, name: "IX_PackageSignatures_EndCertificateKey")
                .Index(t => t.Status, name: "IX_PackageSignatures_Status")
                .Index(t => new { t.Type, t.Status }, name: "IX_PackageSignatures_Type_Status");
            
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
                        Status = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("signature.EndCertificates", t => t.EndCertificateKey)
                .ForeignKey("signature.PackageSignatures", t => t.PackageSignatureKey, cascadeDelete: true)
                .Index(t => t.PackageSignatureKey, unique: true, name: "IX_TrustedTimestamps_PackageSignatureKey")
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
                "dbo.ContentScanOperationStates",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationStepId = c.Guid(nullable: false),
                        Status = c.Int(nullable: false),
                        JobId = c.String(),
                        Type = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        PolledAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        FinishedAt = c.DateTime(precision: 7, storeType: "datetime2"),
                        ContentPath = c.String(),
                        FileId = c.String(maxLength: 64),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => new { t.ValidationStepId, t.Type, t.Status }, name: "IX_ContentScanOperationState_ValidationStepId_Type_StatusIndex")
                .Index(t => t.CreatedAt, name: "IX_ContentScanOperationState_CreatedIndex");
            
            CreateTable(
                "dbo.PackageCompatibilityIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ClientIssueCode = c.String(nullable: false),
                        Message = c.String(nullable: false),
                        PackageValidationKey = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey, cascadeDelete: true)
                .Index(t => t.PackageValidationKey);
            
            CreateTable(
                "dbo.PackageValidations",
                c => new
                    {
                        Key = c.Guid(nullable: false, identity: true),
                        PackageValidationSetKey = c.Long(nullable: false),
                        Type = c.String(nullable: false, maxLength: 255, unicode: false),
                        Started = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidationStatus = c.Int(nullable: false),
                        ValidationStatusTimestamp = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidationSets", t => t.PackageValidationSetKey, cascadeDelete: true)
                .Index(t => t.PackageValidationSetKey);
            
            CreateTable(
                "dbo.PackageValidationIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageValidationKey = c.Guid(nullable: false),
                        IssueCode = c.Int(nullable: false),
                        Data = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey, cascadeDelete: true)
                .Index(t => t.PackageValidationKey);
            
            CreateTable(
                "dbo.PackageValidationSets",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationTrackingId = c.Guid(nullable: false),
                        PackageKey = c.Int(),
                        PackageId = c.String(maxLength: 128),
                        PackageNormalizedVersion = c.String(maxLength: 64),
                        PackageETag = c.String(),
                        PackageContentType = c.String(maxLength: 128),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Updated = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Expiration = c.DateTime(precision: 7, storeType: "datetime2"),
                        ValidatingType = c.Int(nullable: false),
                        ValidationSetStatus = c.Int(nullable: false),
                        ValidationProperties = c.String(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.ValidationTrackingId, unique: true, name: "IX_PackageValidationSets_ValidationTrackingId")
                .Index(t => t.PackageKey, name: "IX_PackageValidationSets_PackageKey")
                .Index(t => new { t.PackageId, t.PackageNormalizedVersion }, name: "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            
            CreateTable(
                "dbo.PackageValidationResults",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        Type = c.String(nullable: false, maxLength: 128),
                        Data = c.String(nullable: false),
                        PackageValidationSetKey = c.Long(nullable: false),
                        PackageValidationKey = c.Guid(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey)
                .ForeignKey("dbo.PackageValidationSets", t => t.PackageValidationSetKey, cascadeDelete: true)
                .Index(t => t.PackageValidationSetKey)
                .Index(t => t.PackageValidationKey);
            
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
                .Index(t => new { t.Enqueued, t.Completed }, name: "IX_PackageRevalidations_Enqueued_Completed")
                .Index(t => t.ValidationTrackingId, unique: true, name: "IX_PackageRevalidations_ValidationTrackingId");
            
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
                        BlobSize = c.Long(),
                        ResultUrl = c.String(maxLength: 2048),
                        OperationId = c.String(maxLength: 64),
                        ErrorCode = c.String(),
                        OperationDetails = c.String(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => new { t.PackageValidationKey, t.OperationType, t.AttemptIndex }, unique: true, name: "IX_ScanOperationStates_PackageValidationKey_OperationType_AttemptIndex")
                .Index(t => new { t.ScanState, t.CreatedAt }, name: "IX_ScanOperationStates_ScanState_Created");
            
            CreateTable(
                "dbo.SymbolsServerRequests",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        SymbolsKey = c.Int(nullable: false),
                        RequestName = c.String(nullable: false),
                        RequestStatusKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.SymbolsKey, name: "IX_SymbolServerRequests_SymbolsKey");
            
            CreateTable(
                "dbo.ValidatorStatuses",
                c => new
                    {
                        ValidationId = c.Guid(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        ValidatorName = c.String(nullable: false),
                        State = c.Int(nullable: false),
                        NupkgUrl = c.String(),
                        BatchId = c.String(maxLength: 20),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                        ValidatingType = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ValidationId)
                .Index(t => t.PackageKey, name: "IX_ValidatorStatuses_PackageKey")
                .Index(t => t.BatchId, name: "IX_ValidatorStatuses_BatchId");
            
            CreateTable(
                "dbo.ValidatorIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationId = c.Guid(nullable: false),
                        IssueCode = c.Int(nullable: false),
                        Data = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.ValidatorStatuses", t => t.ValidationId, cascadeDelete: true)
                .Index(t => t.ValidationId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ValidatorIssues", "ValidationId", "dbo.ValidatorStatuses");
            DropForeignKey("dbo.PackageCompatibilityIssues", "PackageValidationKey", "dbo.PackageValidations");
            DropForeignKey("dbo.PackageValidations", "PackageValidationSetKey", "dbo.PackageValidationSets");
            DropForeignKey("dbo.PackageValidationResults", "PackageValidationSetKey", "dbo.PackageValidationSets");
            DropForeignKey("dbo.PackageValidationResults", "PackageValidationKey", "dbo.PackageValidations");
            DropForeignKey("dbo.PackageValidationIssues", "PackageValidationKey", "dbo.PackageValidations");
            DropForeignKey("signature.CertificateChainLinks", "ParentCertificateKey", "signature.ParentCertificates");
            DropForeignKey("signature.EndCertificateValidations", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.TrustedTimestamps", "PackageSignatureKey", "signature.PackageSignatures");
            DropForeignKey("signature.TrustedTimestamps", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.PackageSignatures", "PackageKey", "signature.PackageSigningStates");
            DropForeignKey("signature.PackageSignatures", "EndCertificateKey", "signature.EndCertificates");
            DropForeignKey("signature.CertificateChainLinks", "EndCertificateKey", "signature.EndCertificates");
            DropIndex("dbo.ValidatorIssues", new[] { "ValidationId" });
            DropIndex("dbo.ValidatorStatuses", "IX_ValidatorStatuses_BatchId");
            DropIndex("dbo.ValidatorStatuses", "IX_ValidatorStatuses_PackageKey");
            DropIndex("dbo.SymbolsServerRequests", "IX_SymbolServerRequests_SymbolsKey");
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_ScanState_Created");
            DropIndex("scan.ScanOperationStates", "IX_ScanOperationStates_PackageValidationKey_OperationType_AttemptIndex");
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_ValidationTrackingId");
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_Enqueued_Completed");
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_PackageId_PackageNormalizedVersion");
            DropIndex("dbo.PackageValidationResults", new[] { "PackageValidationKey" });
            DropIndex("dbo.PackageValidationResults", new[] { "PackageValidationSetKey" });
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageKey");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_ValidationTrackingId");
            DropIndex("dbo.PackageValidationIssues", new[] { "PackageValidationKey" });
            DropIndex("dbo.PackageValidations", new[] { "PackageValidationSetKey" });
            DropIndex("dbo.PackageCompatibilityIssues", new[] { "PackageValidationKey" });
            DropIndex("dbo.ContentScanOperationStates", "IX_ContentScanOperationState_CreatedIndex");
            DropIndex("dbo.ContentScanOperationStates", "IX_ContentScanOperationState_ValidationStepId_Type_StatusIndex");
            DropIndex("signature.ParentCertificates", "IX_ParentCertificates_Thumbprint");
            DropIndex("signature.EndCertificateValidations", "IX_EndCertificateValidations_ValidationId");
            DropIndex("signature.EndCertificateValidations", "IX_EndCertificateValidations_EndCertificateKey_ValidationId");
            DropIndex("signature.TrustedTimestamps", new[] { "EndCertificateKey" });
            DropIndex("signature.TrustedTimestamps", "IX_TrustedTimestamps_PackageSignatureKey");
            DropIndex("signature.PackageSigningStates", "IX_PackageSigningStates_PackageId_PackageNormalizedVersion");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_Type_Status");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_Status");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_EndCertificateKey");
            DropIndex("signature.PackageSignatures", "IX_PackageSignatures_PackageKey_Type");
            DropIndex("signature.EndCertificates", "IX_EndCertificates_Thumbprint");
            DropIndex("signature.CertificateChainLinks", "IX_CertificateChainLinks_EndCertificateKeyParentCertificateKey");
            DropTable("dbo.ValidatorIssues");
            DropTable("dbo.ValidatorStatuses");
            DropTable("dbo.SymbolsServerRequests");
            DropTable("scan.ScanOperationStates");
            DropTable("dbo.PackageRevalidations");
            DropTable("dbo.PackageValidationResults");
            DropTable("dbo.PackageValidationSets");
            DropTable("dbo.PackageValidationIssues");
            DropTable("dbo.PackageValidations");
            DropTable("dbo.PackageCompatibilityIssues");
            DropTable("dbo.ContentScanOperationStates");
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
