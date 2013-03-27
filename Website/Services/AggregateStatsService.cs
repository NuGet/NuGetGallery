using System.Data;
using NuGetGallery.Data;

namespace NuGetGallery
{
    public class AggregateStatsService : IAggregateStatsService
    {
        private readonly IConfiguration _configuration;
        private readonly IEntitiesContextFactory _contextFactory;

        public AggregateStatsService(IConfiguration configuration, IEntitiesContextFactory contextFactory)
        {
            _configuration = configuration;
            _contextFactory = contextFactory;
        }

        public AggregateStats GetAggregateStats()
        {
            using (var dbContext = _contextFactory.Create(readOnly: true)) // true - set readonly but it is ignored anyway, as this class doesn't call EntitiesContext.SaveChanges()
            {
                var database = dbContext.Database;
                using (var command = database.Connection.CreateCommand())
                {
                    command.CommandText = @"SELECT 
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr 
                            WHERE EXISTS (SELECT 1 FROM Packages p WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WHERE Listed = 1) AS TotalPackages,
                    (SELECT TotalDownloadCount FROM GallerySettings) AS DownloadCount";

                    command.CommandTimeout = 200;
                    database.Connection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection | CommandBehavior.SingleRow))
                    {
                        bool hasData = reader.Read();
                        if (!hasData)
                        {
                            return new AggregateStats();
                        }
                        return new AggregateStats
                            {
                                UniquePackages = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                TotalPackages = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                Downloads = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                            };
                    }
                }
            }
        }
    }
}
