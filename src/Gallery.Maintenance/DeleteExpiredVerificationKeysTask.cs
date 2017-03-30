// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Gallery.Maintenance.Models;
using Microsoft.Extensions.Logging;

namespace Gallery.Maintenance
{
    internal class DeleteExpiredVerificationKeysTask : IMaintenanceTask
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

        public async Task<bool> RunAsync(Job job)
        {
            IEnumerable<PackageVerificationKey> expiredKeys;

            using (var connection = await job.GalleryDatabase.ConnectTo())
            {
                expiredKeys = await connection.QueryWithRetryAsync<PackageVerificationKey>(
                    SelectQuery,
                    commandTimeout: _commandTimeout,
                    maxRetries: 3);
            }

            var credentialKeys = expiredKeys.Select(expiredKey =>
            {
                job.Logger.LogInformation(
                    "Found expired verification key: Credential='{credentialKey}' UserKey='{userKey}', User='{userName}', Subject='{scopeSubject}', Expires={expires}",
                    expiredKey.CredentialKey, expiredKey.UserKey, expiredKey.Username, expiredKey.ScopeSubject, expiredKey.Expires);

                return expiredKey.CredentialKey;
            });

            var rowCount = 0;
            var expectedRowCount = expiredKeys.Count() * 2; // credential and scope.

            if (expectedRowCount > 0)
            {
                using (var connection = await job.GalleryDatabase.ConnectTo())
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        rowCount = await connection.ExecuteAsync(
                            string.Format(DeleteQuery, string.Join(",", credentialKeys)),
                            transaction, _commandTimeout);

                        transaction.Commit();
                    }
                }
            }

            job.Logger.LogInformation("Deleted {0} expired verification keys and scopes. Expected={1}.", rowCount, expectedRowCount);

            return rowCount == expectedRowCount;
        }
    }
}
