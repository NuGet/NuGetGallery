namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingEntities : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StagedPackageArtifacts",
                c => new
                    {
                        StagingEntryKey = c.Int(nullable: false),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        BlobETag = c.String(nullable: false, maxLength: 256),
                        ContentHash = c.String(nullable: false, maxLength: 256),
                        Status = c.Int(nullable: false),
                        ValidationTrackingId = c.Guid(nullable: false),
                        UploadedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ValidatedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        PromotionArtifactHistoryKey = c.Int(),
                        PromotionStartedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.StagingEntryKey)
                .ForeignKey("dbo.StagingPromotionArtifactHistories", t => t.PromotionArtifactHistoryKey)
                .ForeignKey("dbo.StagingEntries", t => t.StagingEntryKey)
                .Index(t => t.StagingEntryKey)
                .Index(t => t.ValidationTrackingId);
            
            CreateTable(
                "dbo.StagingPromotionArtifactHistories",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        StagingPromotionHistoryKey = c.Int(nullable: false),
                        PackageKey = c.Int(),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        NormalizedVersion = c.String(nullable: false, maxLength: 64),
                        Kind = c.Int(nullable: false),
                        ContentHash = c.String(nullable: false, maxLength: 256),
                        ValidationTrackingId = c.Guid(nullable: false),
                        SymbolPackageKey = c.Int(),
                        CurrentIngestionValidationTrackingId = c.Guid(),
                        Status = c.Int(nullable: false),
                        StartedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        CompletedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        RetryCount = c.Int(nullable: false),
                        LastRetryDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        LastErrorCode = c.String(maxLength: 128),
                        LastFailureDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        ProcessingLeaseId = c.Guid(),
                        ProcessingLeaseExpiresDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey)
                .ForeignKey("dbo.StagingPromotionHistories", t => t.StagingPromotionHistoryKey, cascadeDelete: true)
                .ForeignKey("dbo.SymbolPackages", t => t.SymbolPackageKey)
                .Index(t => new { t.StagingPromotionHistoryKey, t.PackageId, t.NormalizedVersion, t.Kind }, unique: true)
                .Index(t => t.PackageKey)
                .Index(t => t.SymbolPackageKey);
            
            CreateTable(
                "dbo.StagingPromotionHistories",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Id = c.Guid(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        ApproverUserKey = c.Int(),
                        Scope = c.Int(nullable: false),
                        GroupKey = c.Int(),
                        GroupId = c.String(maxLength: 64),
                        GroupName = c.String(maxLength: 256),
                        RequestedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        CompletedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        Status = c.Int(nullable: false),
                        FailureNotificationQueuedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .Index(t => t.Id, unique: true)
                .Index(t => new { t.OwnerKey, t.RequestedDate })
                .Index(t => t.ApproverUserKey)
                .Index(t => new { t.GroupKey, t.Status });
            
            CreateTable(
                "dbo.StagingGroups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        OwnerKey = c.Int(nullable: false),
                        Id = c.String(nullable: false, maxLength: 64),
                        Name = c.String(nullable: false, maxLength: 256),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .Index(t => new { t.OwnerKey, t.Id }, unique: true)
                .Index(t => t.ExpirationDate);
            
            CreateTable(
                "dbo.StagingEntries",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        StagingGroupKey = c.Int(),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Packages", t => t.PackageKey)
                .ForeignKey("dbo.StagingGroups", t => t.StagingGroupKey)
                .Index(t => t.PackageKey, unique: true)
                .Index(t => new { t.OwnerKey, t.CreatedDate })
                .Index(t => t.StagingGroupKey);
            
            CreateTable(
                "dbo.StagedSymbolArtifacts",
                c => new
                    {
                        StagingEntryKey = c.Int(nullable: false),
                        SymbolPackageKey = c.Int(nullable: false),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        BlobETag = c.String(nullable: false, maxLength: 256),
                        ContentHash = c.String(nullable: false, maxLength: 256),
                        ParentContentHash = c.String(maxLength: 256),
                        Status = c.Int(nullable: false),
                        ValidationTrackingId = c.Guid(nullable: false),
                        UploadedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ValidatedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        PromotionArtifactHistoryKey = c.Int(),
                        PromotionStartedDate = c.DateTime(precision: 7, storeType: "datetime2"),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.StagingEntryKey)
                .ForeignKey("dbo.StagingPromotionArtifactHistories", t => t.PromotionArtifactHistoryKey)
                .ForeignKey("dbo.StagingEntries", t => t.StagingEntryKey)
                .ForeignKey("dbo.SymbolPackages", t => t.SymbolPackageKey)
                .Index(t => t.StagingEntryKey)
                .Index(t => t.SymbolPackageKey, unique: true)
                .Index(t => t.ValidationTrackingId);
            
            CreateTable(
                "dbo.StagingBlobCleanups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        ExpectedETag = c.String(nullable: false, maxLength: 256),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.BlobPath, unique: true);
            
            AddColumn("dbo.Users", "StagingArtifactLimit", c => c.Int());

            Sql(@"CREATE UNIQUE INDEX [IX_StagedPackageArtifacts_PromotionArtifactHistoryKey]
                ON [dbo].[StagedPackageArtifacts] ([PromotionArtifactHistoryKey])
                WHERE [PromotionArtifactHistoryKey] IS NOT NULL");

            Sql(@"CREATE UNIQUE INDEX [IX_StagedSymbolArtifacts_PromotionArtifactHistoryKey]
                ON [dbo].[StagedSymbolArtifacts] ([PromotionArtifactHistoryKey])
                WHERE [PromotionArtifactHistoryKey] IS NOT NULL");

            Sql(@"CREATE INDEX [IX_StagingEntries_ExpirationDate_Ungrouped]
                ON [dbo].[StagingEntries] ([ExpirationDate])
                WHERE [StagingGroupKey] IS NULL");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionHistories] ADD CONSTRAINT [FK_dbo.StagingPromotionHistories_dbo.Users_ApproverUserKey]
                FOREIGN KEY ([ApproverUserKey]) REFERENCES [dbo].[Users] ([Key]) ON DELETE SET NULL");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionHistories] ADD CONSTRAINT [FK_dbo.StagingPromotionHistories_dbo.StagingGroups_GroupKey]
                FOREIGN KEY ([GroupKey]) REFERENCES [dbo].[StagingGroups] ([Key]) ON DELETE SET NULL");

            Sql(@"ALTER TABLE [dbo].[StagedPackageArtifacts] ADD CONSTRAINT [CHK_StagedPackageArtifacts_Lifecycle]
                CHECK (
                    ([Status] IN (0, 1, 2) AND [PromotionArtifactHistoryKey] IS NULL AND [PromotionStartedDate] IS NULL)
                    OR
                    ([Status] IN (3, 4) AND [PromotionArtifactHistoryKey] IS NOT NULL AND [PromotionStartedDate] IS NOT NULL)
                )");

            Sql(@"ALTER TABLE [dbo].[StagedPackageArtifacts] ADD CONSTRAINT [CHK_StagedPackageArtifacts_Validated]
                CHECK ([Status] <> 1 OR [ValidatedDate] IS NOT NULL)");

            Sql(@"ALTER TABLE [dbo].[StagedSymbolArtifacts] ADD CONSTRAINT [CHK_StagedSymbolArtifacts_Lifecycle]
                CHECK (
                    ([Status] IN (0, 1, 2) AND [PromotionArtifactHistoryKey] IS NULL AND [PromotionStartedDate] IS NULL)
                    OR
                    ([Status] IN (3, 4) AND [PromotionArtifactHistoryKey] IS NOT NULL AND [PromotionStartedDate] IS NOT NULL)
                )");

            Sql(@"ALTER TABLE [dbo].[StagedSymbolArtifacts] ADD CONSTRAINT [CHK_StagedSymbolArtifacts_Validated]
                CHECK ([Status] <> 1 OR ([ValidatedDate] IS NOT NULL AND [ParentContentHash] IS NOT NULL))");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionHistories] ADD CONSTRAINT [CHK_StagingPromotionHistories_Scope]
                CHECK (
                    ([Scope] = 0 AND [GroupKey] IS NULL AND [GroupId] IS NULL AND [GroupName] IS NULL)
                    OR
                    ([Scope] = 1 AND [GroupId] IS NOT NULL AND [GroupName] IS NOT NULL)
                )");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionHistories] ADD CONSTRAINT [CHK_StagingPromotionHistories_Lifecycle]
                CHECK (
                    ([Status] = 0 AND [CompletedDate] IS NULL)
                    OR
                    ([Status] IN (1, 2, 3) AND [CompletedDate] IS NOT NULL)
                )");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionArtifactHistories] ADD CONSTRAINT [CHK_StagingPromotionArtifactHistories_Kind]
                CHECK ([Kind] IN (0, 1))");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionArtifactHistories] ADD CONSTRAINT [CHK_StagingPromotionArtifactHistories_Lifecycle]
                CHECK (
                    ([Status] IN (0, 1, 3) AND [CompletedDate] IS NULL)
                    OR
                    ([Status] IN (2, 4) AND [CompletedDate] IS NOT NULL)
                )");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionArtifactHistories] ADD CONSTRAINT [CHK_StagingPromotionArtifactHistories_Failure]
                CHECK ([Status] <> 3 OR [LastFailureDate] IS NOT NULL)");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionArtifactHistories] ADD CONSTRAINT [CHK_StagingPromotionArtifactHistories_Lease]
                CHECK ([ProcessingLeaseId] IS NULL OR [ProcessingLeaseExpiresDate] IS NOT NULL)");

            Sql(@"ALTER TABLE [dbo].[StagingPromotionArtifactHistories] ADD CONSTRAINT [CHK_StagingPromotionArtifactHistories_RetryCount]
                CHECK ([RetryCount] >= 0)");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StagedPackageArtifacts", "StagingEntryKey", "dbo.StagingEntries");
            DropForeignKey("dbo.StagedPackageArtifacts", "PromotionArtifactHistoryKey", "dbo.StagingPromotionArtifactHistories");
            DropForeignKey("dbo.StagingPromotionArtifactHistories", "SymbolPackageKey", "dbo.SymbolPackages");
            DropForeignKey("dbo.StagingPromotionArtifactHistories", "StagingPromotionHistoryKey", "dbo.StagingPromotionHistories");
            DropForeignKey("dbo.StagingPromotionHistories", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagingPromotionHistories", "GroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingGroups", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedSymbolArtifacts", "SymbolPackageKey", "dbo.SymbolPackages");
            DropForeignKey("dbo.StagedSymbolArtifacts", "StagingEntryKey", "dbo.StagingEntries");
            DropForeignKey("dbo.StagedSymbolArtifacts", "PromotionArtifactHistoryKey", "dbo.StagingPromotionArtifactHistories");
            DropForeignKey("dbo.StagingEntries", "StagingGroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingEntries", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.StagingEntries", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagingPromotionHistories", "ApproverUserKey", "dbo.Users");
            DropForeignKey("dbo.StagingPromotionArtifactHistories", "PackageKey", "dbo.Packages");
            DropIndex("dbo.StagingBlobCleanups", new[] { "BlobPath" });
            DropIndex("dbo.StagedSymbolArtifacts", "IX_StagedSymbolArtifacts_PromotionArtifactHistoryKey");
            DropIndex("dbo.StagedSymbolArtifacts", new[] { "ValidationTrackingId" });
            DropIndex("dbo.StagedSymbolArtifacts", new[] { "SymbolPackageKey" });
            DropIndex("dbo.StagedSymbolArtifacts", new[] { "StagingEntryKey" });
            DropIndex("dbo.StagingEntries", new[] { "StagingGroupKey" });
            DropIndex("dbo.StagingEntries", "IX_StagingEntries_ExpirationDate_Ungrouped");
            DropIndex("dbo.StagingEntries", new[] { "OwnerKey", "CreatedDate" });
            DropIndex("dbo.StagingEntries", new[] { "PackageKey" });
            DropIndex("dbo.StagingGroups", new[] { "ExpirationDate" });
            DropIndex("dbo.StagingGroups", new[] { "OwnerKey", "Id" });
            DropIndex("dbo.StagingPromotionHistories", new[] { "GroupKey", "Status" });
            DropIndex("dbo.StagingPromotionHistories", new[] { "ApproverUserKey" });
            DropIndex("dbo.StagingPromotionHistories", new[] { "OwnerKey", "RequestedDate" });
            DropIndex("dbo.StagingPromotionHistories", new[] { "Id" });
            DropIndex("dbo.StagingPromotionArtifactHistories", new[] { "SymbolPackageKey" });
            DropIndex("dbo.StagingPromotionArtifactHistories", new[] { "PackageKey" });
            DropIndex("dbo.StagingPromotionArtifactHistories", new[] { "StagingPromotionHistoryKey", "PackageId", "NormalizedVersion", "Kind" });
            DropIndex("dbo.StagedPackageArtifacts", "IX_StagedPackageArtifacts_PromotionArtifactHistoryKey");
            DropIndex("dbo.StagedPackageArtifacts", new[] { "ValidationTrackingId" });
            DropIndex("dbo.StagedPackageArtifacts", new[] { "StagingEntryKey" });
            DropColumn("dbo.Users", "StagingArtifactLimit");
            DropTable("dbo.StagingBlobCleanups");
            DropTable("dbo.StagedSymbolArtifacts");
            DropTable("dbo.StagingEntries");
            DropTable("dbo.StagingGroups");
            DropTable("dbo.StagingPromotionHistories");
            DropTable("dbo.StagingPromotionArtifactHistories");
            DropTable("dbo.StagedPackageArtifacts");
        }
    }
}
