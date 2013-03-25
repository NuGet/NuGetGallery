namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AggregateStatistics_TotalDownloadCount : SqlResourceMigration
    {
        public AggregateStatistics_TotalDownloadCount()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}
