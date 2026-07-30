namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageStaging : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StagedPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        StagingGroupKey = c.Int(),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ValidationStatus = c.Int(nullable: false),
                        ValidationTrackingId = c.Guid(nullable: false),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        BlobPath = c.String(nullable: false, maxLength: 512),
                        SnupkgBlobPath = c.String(maxLength: 512),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Packages", t => t.PackageKey)
                .ForeignKey("dbo.StagingGroups", t => t.StagingGroupKey)
                .Index(t => t.PackageKey, unique: true)
                .Index(t => t.OwnerKey)
                .Index(t => t.StagingGroupKey)
                .Index(t => t.ExpirationDate);
            
            CreateTable(
                "dbo.StagingGroups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Id = c.String(nullable: false, maxLength: 64),
                        OwnerKey = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 256),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ExpirationDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .Index(t => new { t.OwnerKey, t.Id }, unique: true);
            
            CreateTable(
                "dbo.StagingBlobCleanups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        BlobPath = c.String(nullable: false, maxLength: 512),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key);
            
            AddColumn("dbo.Users", "StagingPackageLimit", c => c.Int());
            AddColumn("dbo.Packages", "ApproverUserKey", c => c.Int());

            // dbo.Packages is one of the largest tables in the gallery database, so the index backing the
            // ApproverUserKey foreign key is created online when the target supports it. The indexes on the
            // staging tables above are left to the scaffolded CreateTable calls because those tables are new
            // and therefore empty.
            // "WITH (ONLINE = ON)" is not supported on all editions of SQL Server. We want to create the index in the background
            // when we are deploying to our live environment on Azure (which supports online index creation).
            // Editions: https://docs.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql?view=sql-server-ver15#arguments
            // We used sp_executesql because it is blocked on SQL that does not support "WITH (ONLINE = ON)".
            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_ApproverUserKey]
                                          ON [dbo].[Packages] ([ApproverUserKey])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_ApproverUserKey]
                      ON [dbo].[Packages] ([ApproverUserKey])
                  END");

            AddForeignKey("dbo.Packages", "ApproverUserKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StagedPackages", "StagingGroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingGroups", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages");
            DropForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.Packages", "ApproverUserKey", "dbo.Users");
            DropIndex("dbo.StagingGroups", new[] { "OwnerKey", "Id" });
            DropIndex("dbo.StagedPackages", new[] { "ExpirationDate" });
            DropIndex("dbo.StagedPackages", new[] { "StagingGroupKey" });
            DropIndex("dbo.StagedPackages", new[] { "OwnerKey" });
            DropIndex("dbo.StagedPackages", new[] { "PackageKey" });
            DropIndex(table: "dbo.Packages", name: "IX_ApproverUserKey");
            DropColumn("dbo.Packages", "ApproverUserKey");
            DropColumn("dbo.Users", "StagingPackageLimit");
            DropTable("dbo.StagingBlobCleanups");
            DropTable("dbo.StagingGroups");
            DropTable("dbo.StagedPackages");
        }
    }
}
