using System.Data;
using System.Threading.Tasks;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class SqlAggregateStatsService : IAggregateStatsService
    {
        readonly IAppConfiguration configuration;

        // Note the NOLOCK hints here!
        private static readonly string GetStatisticsSql = @"SELECT 
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
                            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages,
                    (SELECT TotalDownloadCount FROM GallerySettings WITH (NOLOCK)) AS Downloads";
        
        public SqlAggregateStatsService(IAppConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Task<AggregateStats> GetAggregateStats()
        {
            using (var dbContext = new EntitiesContext(configuration.SqlConnectionString, readOnly: true)) // true - set readonly but it is ignored anyway, as this class doesn't call EntitiesContext.SaveChanges()
            {
                var database = dbContext.Database;
                using (var command = database.Connection.CreateCommand())
                {
                    command.CommandText = GetStatisticsSql;
                    database.Connection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection | CommandBehavior.SingleRow))
                    {
                        bool hasData = reader.Read();
                        if (!hasData)
                        {
                            return Task.FromResult(new AggregateStats());
                        }

                        return Task.FromResult(new AggregateStats
                            {
                                UniquePackages = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                TotalPackages = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                Downloads = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                            });
                    }
                }
            }
        }
    }
}
