// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Gallery.Maintenance.Models;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Configuration;
using NuGetGallery;

namespace Gallery.Maintenance
{
    internal class DeleteExpiredApiKeysTask : MaintenanceTask
    {
        private readonly TimeSpan _commandTimeout = TimeSpan.FromMinutes(5);

        /// Query expired ApiKeys <see cref="CredentialTypes.ApiKey"/> of verify.v1 and v5
        private const string SelectQuery = @"
SELECT s.[CredentialKey], c.[Type] as CredentialType, c.[UserKey], u.[Username], c.[Expires], s.[Subject] as ScopeSubject
FROM [dbo].[Credentials] c
INNER JOIN [dbo].[Scopes] s ON s.[CredentialKey] = c.[Key]
INNER JOIN [dbo].[Users] u ON u.[Key] = c.[UserKey]
WHERE (c.[Type] LIKE 'apikey.verify%' OR c.[Type] = 'apikey.v5')
  AND c.[Expires] < GETUTCDATE()
";

        private const string DeleteQuery = @"
DELETE FROM [dbo].[Scopes] WHERE [CredentialKey] IN ({0})
DELETE FROM [dbo].[Credentials] WHERE [Key] IN ({0})";

        public DeleteExpiredApiKeysTask(ILogger<DeleteExpiredApiKeysTask> logger)
            : base(logger)
        {
        }

        public override async Task RunAsync(Job job)
        {
            IEnumerable<ApiKey> expiredApiKeys;

            using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                expiredApiKeys = await connection.QueryWithRetryAsync<ApiKey>(
                    SelectQuery,
                    commandTimeout: _commandTimeout,
                    maxRetries: 3);
            }

            var credentialKeys = expiredApiKeys.Select(expiredApiKey =>
            {
                _logger.LogInformation(
                    "Found expired ApiKey: CredentialKey='{credentialKey}' CredentialType='{credentialType}' UserKey='{userKey}', User='{userName}', Subject='{scopeSubject}', Expires={expires}",
                    expiredApiKey.CredentialKey, expiredApiKey.CredentialType, expiredApiKey.UserKey, expiredApiKey.Username, expiredApiKey.ScopeSubject, expiredApiKey.Expires);

                return expiredApiKey.CredentialKey;
            });

            var rowCount = 0;
            var expectedRowCount = expiredApiKeys.Count() * 2; // credential and scope.

            if (expectedRowCount > 0)
            {
                using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
                using (var transaction = connection.BeginTransaction())
                using (var command = connection.CreateCommand())
                {
                    var numKeys = 0;
                    var parameters = credentialKeys.Select(c => new SqlParameter("@Key" + numKeys++, SqlDbType.Int) { Value = c }).ToArray();
                    command.Parameters.AddRange(parameters);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                    command.CommandText = string.Format(DeleteQuery, string.Join(",", parameters.Select(p => p.ParameterName)));
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = (int)_commandTimeout.TotalSeconds;
                    command.Transaction = transaction;

                    rowCount = await command.ExecuteNonQueryAsync();

                    transaction.Commit();
                }
            }

            _logger.LogInformation("Deleted {0} expired ApiKeys and Scopes. Expected={1}.", rowCount, expectedRowCount);

            if (expectedRowCount != rowCount)
            {
                throw new Exception($"Expected to delete {expectedRowCount} ApiKeys and Scopes, but only deleted {rowCount}!");
            }
        }
    }
}
