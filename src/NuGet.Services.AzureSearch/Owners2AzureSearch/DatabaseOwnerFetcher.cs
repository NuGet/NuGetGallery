// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class DatabaseOwnerFetcher : IDatabaseOwnerFetcher
    {
        private readonly ISqlConnectionFactory<GalleryDbConfiguration> _connectionFactory;
        private readonly ILogger<DatabaseOwnerFetcher> _logger;

        private const string Sql = @"
SELECT
    pr.Id,
    u.Username
FROM PackageRegistrations pr (NOLOCK)
INNER JOIN PackageRegistrationOwners pro (NOLOCK) ON pro.PackageRegistrationKey = pr.[Key]
INNER JOIN Users u (NOLOCK) ON pro.UserKey = u.[Key]
";

        public DatabaseOwnerFetcher(
            ISqlConnectionFactory<GalleryDbConfiguration> connectionFactory,
            ILogger<DatabaseOwnerFetcher> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger;
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync()
        {
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Sql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var builder = new PackageIdToOwnersBuilder(_logger);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        var username = reader.GetString(1);

                        builder.Add(id, username);
                    }

                    return builder.GetResult();
                }
            }
        }
    }
}

