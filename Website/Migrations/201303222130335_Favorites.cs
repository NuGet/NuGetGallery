namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Favorites : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("UserFollowedPackages", "UserKey", "Users");
            DropForeignKey("UserFollowedPackages", "PackageRegistrationKey", "PackageRegistrations");
            DropIndex("UserFollowedPackages", new[] { "UserKey" });
            DropIndex("UserFollowedPackages", new[] { "PackageRegistrationKey" });
            CreateTable(
                "UserFollowsPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                        IsFollowed = c.Boolean(nullable: false),
                        Created = c.DateTime(nullable: false),
                        LastModified = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .Index(t => t.UserKey)
                .Index(t => t.PackageRegistrationKey);
            
            DropTable("UserFollowedPackages");
        }
        
        public override void Down()
        {
            CreateTable(
                "UserFollowedPackages",
                c => new
                    {
                        UserKey = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserKey, t.PackageRegistrationKey });
            
            DropIndex("UserFollowsPackages", new[] { "PackageRegistrationKey" });
            DropIndex("UserFollowsPackages", new[] { "UserKey" });
            DropForeignKey("UserFollowsPackages", "PackageRegistrationKey", "PackageRegistrations");
            DropForeignKey("UserFollowsPackages", "UserKey", "Users");
            DropTable("UserFollowsPackages");
            CreateIndex("UserFollowedPackages", "PackageRegistrationKey");
            CreateIndex("UserFollowedPackages", "UserKey");
            AddForeignKey("UserFollowedPackages", "PackageRegistrationKey", "PackageRegistrations", "Key", cascadeDelete: true);
            AddForeignKey("UserFollowedPackages", "UserKey", "Users", "Key", cascadeDelete: true);
        }
    }
}
