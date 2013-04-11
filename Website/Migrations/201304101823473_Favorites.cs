namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    //public partial class Favorites : DbMigration
    //{
    //    public override void Up()
    //    {
    //        CreateTable(
    //            "PackageFollows",
    //            c => new
    //                {
    //                    Key = c.Int(nullable: false, identity: true),
    //                    UserKey = c.Int(nullable: false),
    //                    PackageRegistrationKey = c.Int(nullable: false),
    //                    IsFavorited = c.Boolean(nullable: false),
    //                    Created = c.DateTime(nullable: false),
    //                    LastModified = c.DateTime(nullable: false),
    //                })
    //            .PrimaryKey(t => t.Key)
    //            .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
    //            .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
    //            .Index(t => t.UserKey)
    //            .Index(t => t.PackageRegistrationKey);

    //        // There should only ever be one follows relationship per user+packageRegistration pair.
    //        Sql("ALTER TABLE PackageFollows ADD CONSTRAINT UNQ_PackageFollows UNIQUE (UserKey, PackageRegistrationKey)");
    //    }
        
    //    public override void Down()
    //    {
    //        Sql("ALTER TABLE PackageFollows DROP CONSTRAINT UNQ_PackageFollows");

    //        DropIndex("PackageFollows", new[] { "PackageRegistrationKey" });
    //        DropIndex("PackageFollows", new[] { "UserKey" });
    //        DropForeignKey("PackageFollows", "PackageRegistrationKey", "PackageRegistrations");
    //        DropForeignKey("PackageFollows", "UserKey", "Users");
    //        DropTable("PackageFollows");
    //    }
    //}
}
