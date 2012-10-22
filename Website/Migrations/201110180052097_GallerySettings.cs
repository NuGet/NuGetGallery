using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class GallerySettings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "WorkItems",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        JobName = c.String(maxLength: 64),
                        WorkerId = c.String(maxLength: 64),
                        Started = c.DateTime(nullable: false),
                        Completed = c.DateTime(nullable: true),
                        ExceptionInfo = c.String(),
                    })
                .PrimaryKey(t => t.Id);

            Sql("ALTER TABLE WorkItems ADD CONSTRAINT DF_WorkItems_Started DEFAULT getutcdate() FOR Started");
            Sql("ALTER TABLE WorkItems ADD CONSTRAINT DF_WorkItems_Completed DEFAULT getutcdate() FOR Completed");

            CreateTable(
                "GallerySettings",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        DownloadStatsLastAggregatedId = c.Int(nullable: true),
                        SmtpPort = c.Int(),
                        SmtpUsername = c.String(),
                        SmtpHost = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("PackageStatistics", t => t.DownloadStatsLastAggregatedId);

            Sql("ALTER TABLE PackageStatistics ADD CONSTRAINT DF_PackageStatistics_Timestamp DEFAULT getutcdate() FOR Timestamp");
        }

        public override void Down()
        {
            DropTable("GallerySettings");
            DropTable("WorkItems");
            Sql("ALTER TABLE PackageStatistics DROP CONSTRAINT DF_PackageStatistics_Timestamp");
        }
    }
}