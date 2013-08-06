using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class FrameworkName : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageFrameworks",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        TargetFramework = c.String(),
                        Package_Key = c.Int(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.Package_Key)
                .Index(t => t.Package_Key);
        }

        public override void Down()
        {
            DropIndex("PackageFrameworks", new[] { "Package_Key" });
            DropForeignKey("PackageFrameworks", "Package_Key", "Packages");
            DropTable("PackageFrameworks");
        }
    }
}