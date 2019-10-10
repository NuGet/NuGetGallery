// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;

namespace NuGet.Services.DatabaseMigration
{
    public class BaseDbMigrationContext : IMigrationContext
    {
        public SqlConnection SqlConnection { get; set; }
        public Func<DbMigrator> GetDbMigrator { get; set; }
    }
}