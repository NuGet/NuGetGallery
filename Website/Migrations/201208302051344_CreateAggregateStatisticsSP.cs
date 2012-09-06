namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    using System.IO;
    
    public partial class CreateAggregateStatisticsSP : StoredProcedureMigration
    {
        public CreateAggregateStatisticsSP()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}
