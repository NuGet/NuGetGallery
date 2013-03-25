namespace NuGetGallery.Data.Migrations
{
    public partial class AggregateStatistics_TotalDownloadCount : SqlResourceMigration
    {
        public AggregateStatistics_TotalDownloadCount()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}
