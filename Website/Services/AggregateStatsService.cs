﻿using System.Data;

namespace NuGetGallery
{
    public class AggregateStatsService : IAggregateStatsService
    {
        public AggregateStats GetAggregateStats()
        {
            using (var dbContext = new EntitiesContext())
            {
                var database = dbContext.Database;
                using (var command = database.Connection.CreateCommand())
                {
                    command.CommandText = @"Select 
                    (Select Count([Key]) from PackageRegistrations) as UniquePackages,
                    (Select Count([Key]) from Packages where Listed = 1) as TotalPackages,
                    (
                        (Select Sum(DownloadCount) from Packages) + 
                        (Select Count([Key]) from PackageStatistics where [Key] > 
                            (Select DownloadStatsLastAggregatedId FROM GallerySettings)
                        )
                    ) as DownloadCount";
                    
                    database.Connection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection | CommandBehavior.SingleRow))
                    {
                        reader.Read();
                        return new AggregateStats
                        {
                            UniquePackages = reader.GetInt32(0),
                            TotalPackages = reader.GetInt32(1),
                            Downloads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        };
                    }
                }
            }
        }
    }
}