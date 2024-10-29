// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using NuGet.Services.DatabaseMigration;
using NuGetGallery.Migrations;

namespace NuGetGallery.DatabaseMigrationTools
{
    public class GalleryDbMigrationContext : BaseDbMigrationContext
    {
        public GalleryDbMigrationContext(SqlConnection sqlConnection)
        {
            SqlConnection = sqlConnection ?? throw new ArgumentNullException(nameof(sqlConnection));
            SqlConnectionAccessToken = sqlConnection.AccessToken;

            GalleryDbContextFactory.GalleryEntitiesContextFactory = () =>
            {
                SetSqlConnectionAccessToken();
                return new EntitiesContext(SqlConnection, readOnly: false);
            };

            var migrationsConfiguration = new MigrationsConfiguration();
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration, new EntitiesContext(SqlConnection, readOnly: false));
        }
    }
}
