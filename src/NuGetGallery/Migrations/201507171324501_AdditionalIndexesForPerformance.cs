namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AdditionalIndexesForPerformance : DbMigration
    {
        public override void Up()
        {
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistrations_Id_DownloadCount_Key] ON [dbo].[PackageRegistrations] ([Id]) INCLUDE ([DownloadCount], [Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_Packages_IsLatestStable_IsPrerelease] ON [dbo].[Packages] ([IsLatestStable], [IsPrerelease]) INCLUDE ([Description], [FlattenedAuthors], [LastUpdated], [Listed], [PackageRegistrationKey], [Published], [Tags], [Title])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageStatistics_Timestamp] ON [dbo].[PackageStatistics] ([Timestamp]) INCLUDE ([Key])");
        }

        public override void Down()
        {
            DropIndex("PackageRegistrations", name: "IX_PackageRegistrations_Id_DownloadCount_Key");
            DropIndex("Packages", name: "IX_Packages_IsLatestStable_IsPrerelease");
            DropIndex("PackageStatistics", name: "IX_PackageStatistics_Timestamp");
        }
    }
}
