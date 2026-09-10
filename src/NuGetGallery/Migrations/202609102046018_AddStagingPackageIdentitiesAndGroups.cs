namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingPackageIdentitiesAndGroups : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages");
            DropIndex("dbo.StagedPackages", new[] { "PackageKey" });
            DropIndex("dbo.StagedPackages", new[] { "OwnerKey" });
            CreateTable(
                "dbo.StagingPackageIdentities",
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
            
            AddColumn("dbo.StagedPackages", "StagingPackageIdentityKey", c => c.Int(nullable: false));
            CreateIndex("dbo.StagedPackages", "StagingPackageIdentityKey");
            AddForeignKey("dbo.StagedPackages", "StagingPackageIdentityKey", "dbo.StagingPackageIdentities", "Key", cascadeDelete: true);
            DropColumn("dbo.StagedPackages", "PackageKey");
            DropColumn("dbo.StagedPackages", "OwnerKey");
        }
        
        public override void Down()
        {
            AddColumn("dbo.StagedPackages", "OwnerKey", c => c.Int(nullable: false));
            AddColumn("dbo.StagedPackages", "PackageKey", c => c.Int(nullable: false));
            DropForeignKey("dbo.StagedPackages", "StagingPackageIdentityKey", "dbo.StagingPackageIdentities");
            DropForeignKey("dbo.StagingPackageIdentities", "StagingGroupKey", "dbo.StagingGroups");
            DropForeignKey("dbo.StagingGroups", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagingPackageIdentities", "Key", "dbo.Packages");
            DropForeignKey("dbo.StagingPackageIdentities", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.StagingPackageIdentities", "CurrentStagedPackageKey", "dbo.StagedPackages");
            DropIndex("dbo.StagingGroups", "IX_StagingGroups_OwnerKey_Id");
            DropIndex("dbo.StagingPackageIdentities", new[] { "CurrentStagedPackageKey" });
            DropIndex("dbo.StagingPackageIdentities", new[] { "StagingGroupKey" });
            DropIndex("dbo.StagingPackageIdentities", new[] { "OwnerKey" });
            DropIndex("dbo.StagingPackageIdentities", new[] { "Key" });
            DropIndex("dbo.StagedPackages", new[] { "StagingPackageIdentityKey" });
            DropColumn("dbo.StagedPackages", "StagingPackageIdentityKey");
            DropTable("dbo.StagingGroups");
            DropTable("dbo.StagingPackageIdentities");
            CreateIndex("dbo.StagedPackages", "OwnerKey");
            CreateIndex("dbo.StagedPackages", "PackageKey");
            AddForeignKey("dbo.StagedPackages", "PackageKey", "dbo.Packages", "Key", cascadeDelete: true);
            AddForeignKey("dbo.StagedPackages", "OwnerKey", "dbo.Users", "Key");
        }
    }
}
