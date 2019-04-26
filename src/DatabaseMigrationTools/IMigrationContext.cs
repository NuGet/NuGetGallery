// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Migrations;
using System.Data.SqlClient;

namespace NuGetGallery.DatabaseMigrationTools
{
    public interface IMigrationContext
    {
        SqlConnection SqlConnection { get; }
        string SqlConnectionAccessToken { get; }
        DbMigrator Migrator { get; }
        DbMigrator MigratorForScripting { get; }
    }
}
