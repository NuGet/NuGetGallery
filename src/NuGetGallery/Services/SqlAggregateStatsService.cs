// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Threading.Tasks;
using NuGet.Services.Sql;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class SqlAggregateStatsService : IAggregateStatsService
    {
        private readonly IAppConfiguration _configuration;
        private readonly ISqlConnectionFactory _connectionFactory;

        // Note the NOLOCK hints here!
        private const string GetStatisticsSql = @"SELECT
    (SELECT SUM([DownloadCount]) FROM PackageRegistrations WITH (NOLOCK)) As Downloads,
    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1 AND p.PackageStatusKey = 0)) AS UniquePackages,
    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages";

        public SqlAggregateStatsService(IAppConfiguration configuration, ISqlConnectionFactory galleryDbConnectionFactory)
        {
            _configuration = configuration;
            _connectionFactory = galleryDbConnectionFactory;
        }

        public Task<AggregateStats> GetAggregateStats()
        {
            var connection = Task.Run(() => _connectionFactory.CreateAsync()).Result;
            using (var dbContext = new EntitiesContext(connection, readOnly: true)) // true - set readonly but it is ignored anyway, as this class doesn't call EntitiesContext.SaveChanges()
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
                            Downloads = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            UniquePackages = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            TotalPackages = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                        });
                    }
                }
            }
        }
    }
}
