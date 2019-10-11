// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using NuGet.Services.DatabaseMigration;
using NuGet.Services.Validation;

namespace NuGetGallery.DatabaseMigrationTools
{
    class ValidationDbMigrationContext : BaseDbMigrationContext
    {
        public ValidationDbMigrationContext(SqlConnection sqlConnection)
        {
            SqlConnection = sqlConnection ?? throw new ArgumentNullException(nameof(sqlConnection));
            SqlConnectionAccessToken = sqlConnection.AccessToken;

            ValidationDbContextFactory.ValidationEntitiesContextFactory = () =>
            {
                SetSqlConnectionAccessToken();
                return new ValidationEntitiesContext(SqlConnection);
            };

            var migrationsConfiguration = new ValidationMigrationsConfiguration();
            GetDbMigrator = () => new DbMigrator(migrationsConfiguration, new ValidationEntitiesContext(SqlConnection));
        }
    }
}