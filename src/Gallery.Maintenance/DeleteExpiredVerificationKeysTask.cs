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

namespace Gallery.Maintenance
{
    internal class DeleteExpiredVerificationKeysTask : MaintenanceTask
    {
        private readonly TimeSpan _commandTimeout = TimeSpan.FromMinutes(5);

        private const string SelectQuery = @"
SELECT s.[CredentialKey], c.[UserKey], u.[Username], c.[Expires], s.[Subject] as ScopeSubject
FROM [dbo].[Credentials] c
INNER JOIN [dbo].[Scopes] s ON s.[CredentialKey] = c.[Key]
INNER JOIN [dbo].[Users] u ON u.[Key] = c.[UserKey]
WHERE c.[Type] LIKE 'apikey.verify%' AND c.[Expires] < GETUTCDATE()
";

        private const string DeleteQuery = @"
DELETE FROM [dbo].[Scopes] WHERE [CredentialKey] IN ({0})
DELETE FROM [dbo].[Credentials] WHERE [Key] IN ({0})";

        public DeleteExpiredVerificationKeysTask(ILogger<DeleteExpiredVerificationKeysTask> logger)
            : base(logger)
        {
        }

        public override async Task RunAsync(Job job)
        {
            IEnumerable<PackageVerificationKey> expiredKeys;

            using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                expiredKeys = await connection.QueryWithRetryAsync<PackageVerificationKey>(
                    SelectQuery,
                    commandTimeout: _commandTimeout,
                    maxRetries: 3);
            }

            var credentialKeys = expiredKeys.Select(expiredKey =>
            {
                _logger.LogInformation(
                    "Found expired verification key: Credential='{credentialKey}' UserKey='{userKey}', User='{userName}', Subject='{scopeSubject}', Expires={expires}",
                    expiredKey.CredentialKey, expiredKey.UserKey, expiredKey.Username, expiredKey.ScopeSubject, expiredKey.Expires);

                return expiredKey.CredentialKey;
            });

            var rowCount = 0;
            var expectedRowCount = expiredKeys.Count() * 2; // credential and scope.

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

            _logger.LogInformation("Deleted {0} expired verification keys and scopes. Expected={1}.", rowCount, expectedRowCount);

            if (expectedRowCount != rowCount)
            {
                throw new Exception($"Expected to delete {expectedRowCount} verification keys, but only deleted {rowCount}!");
            }
        }
    }
}
