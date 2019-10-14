// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;

namespace NuGet.Services.DatabaseMigration
{
    public class BaseDbMigrationContext : IMigrationContext
    {
        public SqlConnection SqlConnection { get; set; }
        public string SqlConnectionAccessToken { get; set; }
        public Func<DbMigrator> GetDbMigrator { get; set; }

        public void SetSqlConnectionAccessToken()
        {
            if (SqlConnection.State == ConnectionState.Closed)
            {
                // Reset the access token if the connection is closed to ensure connection authentication.
                SqlConnection.AccessToken = SqlConnectionAccessToken;
            }
        }

        public void Dispose()
        {
            SqlConnection?.Dispose();
        }
    }
}
