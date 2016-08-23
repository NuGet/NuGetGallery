// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Threading.Tasks;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class SqlAggregateStatsService : IAggregateStatsService
    {
        private readonly IGalleryConfigurationService _configService;

        // Note the NOLOCK hints here!
        private static readonly string GetStatisticsSql = @"SELECT
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
                            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1 AND p.PackageDelete_Key IS NULL)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages";

        public SqlAggregateStatsService(IGalleryConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            using (var dbContext = new EntitiesContext((await _configService.GetCurrent()).SqlConnectionString, readOnly: true)) // true - set readonly but it is ignored anyway, as this class doesn't call EntitiesContext.SaveChanges()
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
                            return new AggregateStats();
                        }

                        return new AggregateStats
                            {
                                UniquePackages = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                TotalPackages = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                            };
                    }
                }
            }
        }
    }
}
