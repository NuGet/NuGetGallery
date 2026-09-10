namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackageIdentitiesAndGroups : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages");
            DropIndex("dbo.StagedPackages", new[] { "PackageKey" });
            DropIndex("dbo.StagedPackages", new[] { "OwnerKey" });
            CreateTable(
                "dbo.StagedPackageIdentities",
                c => new
                    {
                        Key = c.Int(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                        StagingGroupKey = c.Int(),
                        CurrentStagedPackageKey = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.StagedPackages", t => t.CurrentStagedPackageKey)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .ForeignKey("dbo.StagingGroups", t => t.StagingGroupKey)
                .Index(t => t.Key)
                .Index(t => t.OwnerKey)
                .Index(t => t.StagingGroupKey)
                .Index(t => t.CurrentStagedPackageKey);
            
            CreateTable(
                "dbo.StagingGroups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        OwnerKey = c.Int(nullable: false),
                        Id = c.String(nullable: false, maxLength: 64),
                        Name = c.String(nullable: false, maxLength: 128),
                        CreatedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .Index(t => new { t.OwnerKey, t.Id }, unique: true, name: "IX_StagingGroups_OwnerKey_Id");
            
            AddColumn("dbo.StagedPackages", "StagedPackageIdentityKey", c => c.Int(nullable: false));
            CreateIndex("dbo.StagedPackages", "StagedPackageIdentityKey");
            AddForeignKey("dbo.StagedPackages", "StagedPackageIdentityKey", "dbo.StagedPackageIdentities", "Key", cascadeDelete: true);
            DropColumn("dbo.StagedPackages", "PackageKey");
            DropColumn("dbo.StagedPackages", "OwnerKey");
        }
        
        public override void Down()
        {
            AddColumn("dbo.StagedPackages", "OwnerKey", c => c.Int(nullable: false));
            AddColumn("dbo.StagedPackages", "PackageKey", c => c.Int(nullable: false));
            DropForeignKey("dbo.StagedPackageIdentities", "StagingGroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingGroups", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackageIdentities", "Key", "dbo.Packages");
            DropForeignKey("dbo.StagedPackageIdentities", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackageIdentities", "CurrentStagedPackageKey", "dbo.StagedPackages");
            DropForeignKey("dbo.StagedPackages", "StagedPackageIdentityKey", "dbo.StagedPackageIdentities");
            DropIndex("dbo.StagingGroups", "IX_StagingGroups_OwnerKey_Id");
            DropIndex("dbo.StagedPackages", new[] { "StagedPackageIdentityKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "CurrentStagedPackageKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "StagingGroupKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "OwnerKey" });
            DropIndex("dbo.StagedPackageIdentities", new[] { "Key" });
            DropColumn("dbo.StagedPackages", "StagedPackageIdentityKey");
            DropTable("dbo.StagingGroups");
            DropTable("dbo.StagedPackageIdentities");
            CreateIndex("dbo.StagedPackages", "OwnerKey");
            CreateIndex("dbo.StagedPackages", "PackageKey");
            AddForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages", "Key", cascadeDelete: true);
            AddForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users", "Key");
        }
    }
}
