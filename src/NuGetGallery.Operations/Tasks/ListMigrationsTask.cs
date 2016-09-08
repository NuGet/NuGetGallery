// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Data.Entity.Migrations;
using System.IO;
using System.Reflection;
using NuGetGallery.Operations.Common;
using System.Data.Entity.Migrations.Infrastructure;

namespace NuGetGallery.Operations.Tasks
{
    [Command("listmigrations", "Lists migrations in the specified assembly/database combination", AltName = "lm", MaxArgs = 0)]
    public class ListMigrationsTask : MigrationsTask
    {
        protected override void ExecuteCommandCore(MigratorBase migrator)
        {
            Log.Info("Migration Status for {0} on {1}",
                     ConnectionString.InitialCatalog,
                     ConnectionString.DataSource);

            foreach (var migration in migrator.GetDatabaseMigrations().Reverse())
            {
                Log.Info("A {0}", migration);
            }

            foreach (var migration in migrator.GetPendingMigrations())
            {
                Log.Info("  {0}", migration);
            }
            Log.Info("A = Applied");
        }
    }
}
