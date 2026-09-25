namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddStagedSymbolPackages : DbMigration
    {
        public override void Up()
        {
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
                        UploadedDate = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.StagedPackageIdentities", t => t.StagedPackageIdentityKey, cascadeDelete: true)
                .ForeignKey("dbo.SymbolPackages", t => t.SymbolPackageKey)
                .Index(t => t.SymbolPackageKey)
                .Index(t => t.StagedPackageIdentityKey);

            AddColumn("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey", c => c.Int());
            CreateIndex("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey");
            AddForeignKey("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey", "dbo.StagedSymbolPackages", "Key");
        }

        public override void Down()
        {
            DropForeignKey("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey", "dbo.StagedSymbolPackages");
            DropForeignKey("dbo.StagedSymbolPackages", "SymbolPackageKey", "dbo.SymbolPackages");
            DropForeignKey("dbo.StagedSymbolPackages", "StagedPackageIdentityKey", "dbo.StagedPackageIdentities");
            DropIndex("dbo.StagedSymbolPackages", new[] { "StagedPackageIdentityKey" });
            DropIndex("dbo.StagedSymbolPackages", new[] { "SymbolPackageKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "CurrentStagedSymbolPackageKey" });
            DropColumn("dbo.StagedPackageIdentities", "CurrentStagedSymbolPackageKey");
            DropTable("dbo.StagedSymbolPackages");
        }
    }
}
