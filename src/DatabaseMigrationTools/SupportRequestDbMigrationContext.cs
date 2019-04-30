// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using NuGet.Services.DatabaseMigration;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.DatabaseMigrationTools
{
    public class SupportRequestDbMigrationContext : IMigrationContext
    {
        public SqlConnection SqlConnection { get; }
        public string SqlConnectionAccessToken { get; }
        public Func<DbMigrator> GetDbMigrator { get; }

        public SupportRequestDbMigrationContext(SqlConnection sqlConnection)
        {
            SqlConnection = sqlConnection ?? throw new ArgumentNullException(nameof(sqlConnection));
            SqlConnectionAccessToken = sqlConnection.AccessToken;

            SupportRequestDbContextFactory.SupportRequestEntitiesContextFactory = () =>
            {
                if (SqlConnection.State == ConnectionState.Closed)
                {
                    // Reset the access token if the connection is closed to ensure connection authentication.
                    SqlConnection.AccessToken = SqlConnectionAccessToken;
                }

                return new SupportRequestDbContext(SqlConnection);
            };

            var migrationsConfiguration = new SupportRequestMigrationsConfiguration();
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration);
        }
    }
}
