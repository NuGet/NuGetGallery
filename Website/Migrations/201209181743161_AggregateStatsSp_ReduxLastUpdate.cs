namespace NuGetGallery.Migrations
{
    public partial class AggregateStatsSp_ReduxLastUpdate : SqlResourceMigration
    {
        public AggregateStatsSp_ReduxLastUpdate()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}