// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using NuGet.Services.DatabaseMigration;
using NuGet.Services.CatalogValidation;
using NuGet.Services.CatalogValidation.Entities;

namespace NuGetGallery.DatabaseMigrationTools
{
    class CatalogValidationDbMigrationContext : BaseDbMigrationContext
    {
        public CatalogValidationDbMigrationContext(SqlConnection sqlConnection)
        {
            SqlConnection = sqlConnection ?? throw new ArgumentNullException(nameof(sqlConnection));
            SqlConnectionAccessToken = sqlConnection.AccessToken;

            CatalogValidationDbContextFactory.CatalogValidationEntitiesContextFactory = () =>
            {
                SetSqlConnectionAccessToken();
                return new CatalogValidationEntitiesContext(SqlConnection);
            };

            var migrationsConfiguration = new CatalogValidationMigrationsConfiguration();
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration, new CatalogValidationEntitiesContext(SqlConnection));
        }
    }
}
