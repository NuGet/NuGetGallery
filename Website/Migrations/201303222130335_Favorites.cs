namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Favorites : DbMigration
    {
        public override void Up()
        {
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
        }
        
        public override void Down()
        {
            DropIndex("UserFollowsPackages", new[] { "PackageRegistrationKey" });
            DropIndex("UserFollowsPackages", new[] { "UserKey" });
            DropForeignKey("UserFollowsPackages", "PackageRegistrationKey", "PackageRegistrations");
            DropForeignKey("UserFollowsPackages", "UserKey", "Users");
            DropTable("UserFollowsPackages");
        }
    }
}
