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

            var migrationsConfiguration = new MigrationsConfiguration();
            var migrationsDbContext = new EntitiesContext(sqlConnection, false);
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration, migrationsDbContext);
        }
    }
}
