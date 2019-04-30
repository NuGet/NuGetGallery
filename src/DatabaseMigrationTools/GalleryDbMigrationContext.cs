// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using NuGet.Services.DatabaseMigration;
using NuGetGallery.Migrations;

namespace NuGetGallery.DatabaseMigrationTools
{
    public class GalleryDbMigrationContext : IMigrationContext
    {
        public SqlConnection SqlConnection { get; }
        public string SqlConnectionAccessToken { get; }
        public Func<DbMigrator> GetDbMigrator { get; }

        public GalleryDbMigrationContext(SqlConnection sqlConnection)
        {
            SqlConnection = sqlConnection ?? throw new ArgumentNullException(nameof(sqlConnection));
            SqlConnectionAccessToken = sqlConnection.AccessToken;

            GalleryDbContextFactory.GalleryEntitiesContextFactory = () =>
            {
                if (SqlConnection.State == ConnectionState.Closed)
                {
                    // Reset the access token if the connection is closed to ensure connection authentication.
                    SqlConnection.AccessToken = SqlConnectionAccessToken;
                }

                return new EntitiesContext(SqlConnection, readOnly: false);
            };

            var migrationsConfiguration = new MigrationsConfiguration();
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration);
        }
    }
}
