using System.Data;

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
                    (Select Count([Key]) from PackageRegistrations pr 
                            where exists (Select 1 from Packages p where p.PackageRegistrationKey = pr.[Key] and p.Listed = 1)) as UniquePackages,
                    (Select Count([Key]) from Packages where Listed = 1) as TotalPackages,
                    (Select Sum(DownloadCount) from Packages) as DownloadCount";
                    
                    database.Connection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection | CommandBehavior.SingleRow))
                    {
                        bool hasData = reader.Read();
                        if (!hasData)
                            return new AggregateStats();
                        return new AggregateStats
                        {
                            UniquePackages = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            TotalPackages = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            Downloads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        };
                    }
                }
            }
        }
    }
}