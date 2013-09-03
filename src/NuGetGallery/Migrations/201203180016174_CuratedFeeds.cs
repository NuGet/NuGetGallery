using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class CuratedFeeds : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "CuratedFeeds",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Id = c.String(),
                    })
                .PrimaryKey(t => t.Key);

            CreateTable(
                "CuratedPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        CuratedFeedKey = c.Int(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("CuratedFeeds", t => t.CuratedFeedKey, cascadeDelete: true)
                .Index(t => t.PackageKey)
                .Index(t => t.CuratedFeedKey);

            CreateTable(
                "CuratedFeedManagers",
                c => new
                    {
                        CuratedFeedKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.CuratedFeedKey, t.UserKey })
                .ForeignKey("CuratedFeeds", t => t.CuratedFeedKey, cascadeDelete: true)
                .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.CuratedFeedKey)
                .Index(t => t.UserKey);
        }

        public override void Down()
        {
            DropIndex("CuratedFeedManagers", new[] { "UserKey" });
            DropIndex("CuratedFeedManagers", new[] { "CuratedFeedKey" });
            DropIndex("CuratedPackages", new[] { "CuratedFeedKey" });
            DropIndex("CuratedPackages", new[] { "PackageKey" });
            DropForeignKey("CuratedFeedManagers", "UserKey", "Users");
            DropForeignKey("CuratedFeedManagers", "CuratedFeedKey", "CuratedFeeds");
            DropForeignKey("CuratedPackages", "CuratedFeedKey", "CuratedFeeds");
            DropForeignKey("CuratedPackages", "PackageKey", "Packages");
            DropTable("CuratedFeedManagers");
            DropTable("CuratedPackages");
            DropTable("CuratedFeeds");
        }
    }
}