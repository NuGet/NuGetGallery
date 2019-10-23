// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;

namespace NuGet.Services.DatabaseMigration
{
    /// <summary>
    /// This interface is used to define the context needed for database migration.
    /// </summary>
    public interface IMigrationContext : IDisposable
    {
        /// <summary>
        /// SqlConnection to the target database.
        /// </summary>
        SqlConnection SqlConnection { get; }
        /// <summary>
        /// Access token (AAD) for connection authentication.
        /// </summary>
        string SqlConnectionAccessToken { get; }
        /// <summary>
        /// The Func to get the DbMigrator which executes the migration.
        /// </summary>
        Func<DbMigrator> GetDbMigrator { get; }
    }
}
