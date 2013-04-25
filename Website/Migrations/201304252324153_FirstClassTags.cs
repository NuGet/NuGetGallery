namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class FirstClassTags : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Tags",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 64),
                        Description = c.String(maxLength: 1024),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Name, unique: true);

            CreateTable(
                "dbo.PackageTags",
                c => new
                    {
                        PackageRegistrationKey = c.Int(nullable: false),
                        TagKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistrationKey, t.TagKey })
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.Tags", t => t.TagKey, cascadeDelete: true)
                .Index(t => t.PackageRegistrationKey)
                .Index(t => t.TagKey);

            AddColumn("dbo.PackageRegistrations", "FlattenedTags", c => c.String(maxLength: 1024));
        }

        public override void Down()
        {
            DropIndex("dbo.Tags", new[] { "Name" });
            DropIndex("dbo.PackageTags", new[] { "TagKey" });
            DropIndex("dbo.PackageTags", new[] { "PackageRegistrationKey" });
            DropForeignKey("dbo.PackageTags", "TagKey", "dbo.Tags");
            DropForeignKey("dbo.PackageTags", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropColumn("dbo.PackageRegistrations", "FlattenedTags");
            DropTable("dbo.PackageTags");
            DropTable("dbo.Tags");
        }
    }
}
