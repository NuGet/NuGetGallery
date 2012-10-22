namespace NuGetGallery.Migrations
{
    public partial class CreateAggregateStatisticsSP : SqlResourceMigration
    {
        public CreateAggregateStatisticsSP()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}