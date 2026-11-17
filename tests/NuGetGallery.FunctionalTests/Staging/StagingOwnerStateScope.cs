// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Staging
{
    /// <summary>
    /// Temporarily restricts the dedicated local staging owner and restores its original state.
    /// </summary>
    internal sealed class StagingOwnerStateScope : IAsyncDisposable
    {
        private const int LockedUserStatus = 2;

        private readonly int _ownerKey;

        private readonly string _emailAddress;

        private readonly int _userStatus;

        private StagingOwnerStateScope(int ownerKey, string emailAddress, int userStatus)
        {
            _ownerKey = ownerKey;
            _emailAddress = emailAddress;
            _userStatus = userStatus;
        }

        internal static async Task<StagingOwnerStateScope> RestrictAsync(bool locked)
        {
            Assert.Equal("ci-gallery", Environment.GetEnvironmentVariable("APPHOST_PROFILE"));
            var owner = GalleryConfiguration.Instance.StagingRestrictedOrganization;
            Assert.Equal("NugetTestStagingRestrictedOrganization", owner.Name);
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            using var command = new SqlCommand("SELECT [Key], EmailAddress, UserStatusKey FROM Users WITH (UPDLOCK) WHERE Username = @username", connection, transaction);
            command.Parameters.Add("@username", SqlDbType.NVarChar, 64).Value = owner.Name;
            StagingOwnerStateScope scope;
            using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync(), "The dedicated staging owner was not seeded.");
                Assert.False(reader.IsDBNull(1), "The fixture owner must initially be confirmed.");
                scope = new StagingOwnerStateScope(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
                Assert.Equal(0, scope._userStatus);
            }

            var emailAddress = scope._emailAddress;
            var userStatus = scope._userStatus;
            if (locked)
            {
                userStatus = LockedUserStatus;
            }
            else
            {
                emailAddress = null;
            }

            using var update = CreateUpdate(connection, transaction, scope._ownerKey, emailAddress, userStatus);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
            transaction.Commit();
            return scope;
        }

        public async ValueTask DisposeAsync()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var command = CreateUpdate(connection, null, _ownerKey, _emailAddress, _userStatus);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        private static SqlConnection CreateConnection()
        {
            var connectionString = GalleryConfiguration.Instance.StagingDatabaseConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "The local fixture must provide its staging database connection.");
            return new SqlConnection(connectionString);
        }

        private static SqlCommand CreateUpdate(SqlConnection connection, SqlTransaction transaction, int ownerKey, string emailAddress, int userStatus)
        {
            var command = new SqlCommand("UPDATE Users SET EmailAddress = @email, UserStatusKey = @status WHERE [Key] = @key", connection, transaction);
            command.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = (object)emailAddress ?? DBNull.Value;
            command.Parameters.Add("@status", SqlDbType.Int).Value = userStatus;
            command.Parameters.Add("@key", SqlDbType.Int).Value = ownerKey;
            return command;
        }
    }
}
