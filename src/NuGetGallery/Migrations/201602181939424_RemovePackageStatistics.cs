using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class RemovePackageStatistics : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("GallerySettings", "DownloadStatsLastAggregatedId", "PackageStatistics");
            DropForeignKey("dbo.PackageStatistics", "PackageKey", "dbo.Packages");
            DropIndex("dbo.PackageStatistics", new[] { "PackageKey" });
            DropTable("dbo.PackageStatistics");
        }

        public override void Down()
        {
            CreateTable(
                "dbo.PackageStatistics",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        IPAddress = c.String(),
                        UserAgent = c.String(),
                        Operation = c.String(maxLength: 128),
                        DependentPackage = c.String(maxLength: 128),
                        ProjectGuids = c.String(),
                    })
                .PrimaryKey(t => t.Key);

            CreateIndex("dbo.PackageStatistics", "PackageKey");
            AddForeignKey("dbo.PackageStatistics", "PackageKey", "dbo.Packages", "Key", cascadeDelete: true);
            AddForeignKey("GallerySettings", "DownloadStatsLastAggregatedId", "PackageStatistics", "Key", cascadeDelete: true);
        }
    }
}
