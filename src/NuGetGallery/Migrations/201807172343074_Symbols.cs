namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class Symbols : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Symbols",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        Published = c.DateTime(),
                        FileSize = c.Long(nullable: false),
                        Hash = c.String(),
                        StatusKey = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);
        }

        public override void Down()
        {
            DropForeignKey("dbo.Symbols", "PackageKey", "dbo.Packages");
            DropIndex("dbo.Symbols", new[] { "PackageKey" });
            DropTable("dbo.Symbols");
        }
    }
}
