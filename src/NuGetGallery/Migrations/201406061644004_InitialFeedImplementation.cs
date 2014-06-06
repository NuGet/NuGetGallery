namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialFeedImplementation : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Feeds",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(maxLength: 128),
                        Inclusive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.FeedPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        FeedKey = c.Int(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        IsLatest = c.Boolean(nullable: false),
                        IsLatestStable = c.Boolean(nullable: false),
                        Added = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("dbo.Feeds", t => t.FeedKey, cascadeDelete: true)
                .Index(t => t.PackageKey)
                .Index(t => t.FeedKey);
            
            CreateTable(
                "dbo.FeedRules",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        FeedKey = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                        PackageVersionSpec = c.String(maxLength: 256),
                        Notes = c.String(maxLength: 512),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.Feeds", t => t.FeedKey, cascadeDelete: true)
                .Index(t => t.PackageRegistrationKey)
                .Index(t => t.FeedKey);
            
            CreateTable(
                "dbo.FeedManagers",
                c => new
                    {
                        FeedKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.FeedKey, t.UserKey })
                .ForeignKey("dbo.Feeds", t => t.FeedKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.FeedKey)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.FeedManagers", new[] { "UserKey" });
            DropIndex("dbo.FeedManagers", new[] { "FeedKey" });
            DropIndex("dbo.FeedRules", new[] { "FeedKey" });
            DropIndex("dbo.FeedRules", new[] { "PackageRegistrationKey" });
            DropIndex("dbo.FeedPackages", new[] { "FeedKey" });
            DropIndex("dbo.FeedPackages", new[] { "PackageKey" });
            DropForeignKey("dbo.FeedManagers", "UserKey", "dbo.Users");
            DropForeignKey("dbo.FeedManagers", "FeedKey", "dbo.Feeds");
            DropForeignKey("dbo.FeedRules", "FeedKey", "dbo.Feeds");
            DropForeignKey("dbo.FeedRules", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.FeedPackages", "FeedKey", "dbo.Feeds");
            DropForeignKey("dbo.FeedPackages", "PackageKey", "dbo.Packages");
            DropTable("dbo.FeedManagers");
            DropTable("dbo.FeedRules");
            DropTable("dbo.FeedPackages");
            DropTable("dbo.Feeds");
        }
    }
}
