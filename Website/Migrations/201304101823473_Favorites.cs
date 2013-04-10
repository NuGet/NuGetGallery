namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Favorites : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageFavorites",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                        IsFavorited = c.Boolean(nullable: false),
                        Created = c.DateTime(nullable: false),
                        LastModified = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .Index(t => t.UserKey)
                .Index(t => t.PackageRegistrationKey);

            // There should only ever be one follows relationship per user+packageRegistration pair.
            Sql("ALTER TABLE PackageFavorites ADD CONSTRAINT UNQ_PackageFavorites UNIQUE (UserKey, PackageRegistrationKey)");
        }
        
        public override void Down()
        {
            Sql("ALTER TABLE PackageFavorites DROP CONSTRAINT UNQ_PackageFavorites");

            DropIndex("PackageFavorites", new[] { "PackageRegistrationKey" });
            DropIndex("PackageFavorites", new[] { "UserKey" });
            DropForeignKey("PackageFavorites", "PackageRegistrationKey", "PackageRegistrations");
            DropForeignKey("PackageFavorites", "UserKey", "Users");
            DropTable("PackageFavorites");
        }
    }
}
