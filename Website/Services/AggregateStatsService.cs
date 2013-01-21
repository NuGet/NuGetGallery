﻿using System.Data;

namespace NuGetGallery
{
    public class AggregateStatsService : IAggregateStatsService
    {
        readonly IConfiguration configuration;

        public AggregateStatsService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public AggregateStats GetAggregateStats()
        {
            using (var dbContext = new EntitiesContext(configuration.SqlConnectionString))
            {
                var database = dbContext.Database;
                using (var command = database.Connection.CreateCommand())
                {
                    command.CommandText = @"SELECT 
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr 
                            WHERE EXISTS (SELECT 1 FROM Packages p WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WHERE Listed = 1) AS TotalPackages,
                    (SELECT TotalDownloadCount FROM GallerySettings) AS DownloadCount";

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