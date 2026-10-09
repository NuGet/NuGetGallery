namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Adds the complete package staging schema.
    /// </summary>
    public partial class AddPackageStaging : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StagedPackageIdentities",
                c => new
                    {
                        Key = c.Int(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        StagingGroupKey = c.Int(),
                        CurrentStagedPackageKey = c.Int(),
                        CurrentStagedSymbolPackageKey = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.StagedPackages", t => t.CurrentStagedPackageKey)
                .ForeignKey("dbo.StagedSymbolPackages", t => t.CurrentStagedSymbolPackageKey)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .ForeignKey("dbo.StagingGroups", t => t.StagingGroupKey)
                .Index(t => t.Key)
                .Index(t => t.OwnerKey)
                .Index(t => t.StagingGroupKey)
                .Index(t => t.CurrentStagedPackageKey)
                .Index(t => t.CurrentStagedSymbolPackageKey);
            
            CreateTable(
                "dbo.StagedPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        StagedPackageIdentityKey = c.Int(nullable: false, defaultValue: 0),
                        UploadedBlobPath = c.String(nullable: false, maxLength: 256),
                        UploadedBlobETag = c.String(nullable: false, maxLength: 256, defaultValue: ""),
                        ValidatedBlobPath = c.String(maxLength: 256),
                        ValidatedBlobETag = c.String(maxLength: 256),
                        UploadHash = c.String(nullable: false, maxLength: 256, defaultValue: ""),
                        Status = c.Int(nullable: false, defaultValue: 0),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2", defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())"),
                        WarnedExpirationDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        MutationRevision = c.Long(nullable: false, defaultValue: 0),
                        ActivePromotionId = c.Guid(),
                        PromotionMessageSentDate = c.DateTime(),
                        UploadedDate = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.StagedPackageIdentities", t => t.StagedPackageIdentityKey, cascadeDelete: true)
                .Index(t => t.StagedPackageIdentityKey);
            
            CreateTable(
                "dbo.StagedSymbolPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        SymbolPackageKey = c.Int(nullable: false),
                        StagedPackageIdentityKey = c.Int(nullable: false),
                        UploadedBlobPath = c.String(nullable: false, maxLength: 256),
                        UploadedBlobETag = c.String(nullable: false, maxLength: 256),
                        Status = c.Int(nullable: false),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2", defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())"),
                        WarnedExpirationDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        UploadedDate = c.DateTime(nullable: false),
                        ActivePromotionId = c.Guid(),
                        PromotionMessageSentDate = c.DateTime(),
                        MutationRevision = c.Int(nullable: false, defaultValue: 0),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.StagedPackageIdentities", t => t.StagedPackageIdentityKey, cascadeDelete: true)
                .ForeignKey("dbo.SymbolPackages", t => t.SymbolPackageKey)
                .Index(t => t.SymbolPackageKey)
                .Index(t => t.StagedPackageIdentityKey);
            
            CreateTable(
                "dbo.StagingGroups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        OwnerKey = c.Int(nullable: false),
                        Id = c.String(nullable: false, maxLength: 64),
                        Name = c.String(nullable: false, maxLength: 128),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2", defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())"),
                        WarnedExpirationDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        MutationRevision = c.Long(nullable: false, defaultValue: 0),
                        ActivePromotionId = c.Guid(),
                        PromotionMessageSentDate = c.DateTime(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .Index(t => new { t.OwnerKey, t.Id }, unique: true, name: "IX_StagingGroups_OwnerKey_Id");
            
            CreateTable(
                "dbo.StagingBlobCleanups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        StagedPackageIdentityKey = c.Int(nullable: false),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        BlobETag = c.String(nullable: false, maxLength: 256),
                        QueuedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key);
            
            AddColumn("dbo.Users", "NotifyPackageStaged", c => c.Boolean(nullable: false, defaultValue: true));
            Sql("UPDATE dbo.Users SET NotifyPackageStaged = 0 WHERE IsDeleted = 1");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StagedPackageIdentities", "StagingGroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingGroups", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackageIdentities", "Key", "dbo.Packages");
            DropForeignKey("dbo.StagedPackageIdentities", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey", "dbo.StagedSymbolPackages");
            DropForeignKey("dbo.StagedSymbolPackages", "SymbolPackageKey", "dbo.SymbolPackages");
            DropForeignKey("dbo.StagedSymbolPackages", "StagedPackageIdentityKey", "dbo.StagedPackageIdentities");
            DropForeignKey("dbo.StagedPackageIdentities", "CurrentStagedPackageKey", "dbo.StagedPackages");
            DropForeignKey("dbo.StagedPackages", "StagedPackageIdentityKey", "dbo.StagedPackageIdentities");
            DropIndex("dbo.StagingGroups", "IX_StagingGroups_OwnerKey_Id");
            DropIndex("dbo.StagedSymbolPackages", new[] { "StagedPackageIdentityKey" });
            DropIndex("dbo.StagedSymbolPackages", new[] { "SymbolPackageKey" });
            DropIndex("dbo.StagedPackages", new[] { "StagedPackageIdentityKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "CurrentStagedSymbolPackageKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "CurrentStagedPackageKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "StagingGroupKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "OwnerKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "Key" });
            DropColumn("dbo.Users", "NotifyPackageStaged");
            DropTable("dbo.StagingBlobCleanups");
            DropTable("dbo.StagingGroups");
            DropTable("dbo.StagedSymbolPackages");
            DropTable("dbo.StagedPackages");
            DropTable("dbo.StagedPackageIdentities");
        }
    }
}
