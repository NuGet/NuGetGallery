// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("runmigrations", "Executes migrations against a database", AltName = "rm", MaxArgs = 0)]
    public class RunMigrationsTask : MigrationsTask
    {
        private static readonly Regex MigrationIdRegex = new Regex(@"^(?<timestamp>\d+)_(?<name>.*)$");

        [Option("The target to migrate the database to. Timestamp does not need to be specified.", AltName = "m")]
        public string TargetMigration { get; set; }

        [Option("Set this to generate a SQL File instead of running the migration. The file will be dropped in the current directory and named [Source]-[TargetMigration].sql")]
        public bool Sql { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(TargetMigration, "TargetMigration");
        }

        protected override void ExecuteCommandCore(MigratorBase migrator)
        {
            if (Sql)
            {
                ScriptMigrations(migrator);
            }
            else
            {
                RunMigrations(migrator);
            }
        }

        private void ScriptMigrations(MigratorBase migrator)
        {
            var scriptingMigrator = new MigratorScriptingDecorator(migrator);

            string start;
            string target;
            if (migrator.GetDatabaseMigrations().Any(s => IsMigration(s, TargetMigration)))
            {
                // Down migration, start is null, target is the target
                start = null;
                target = migrator.GetDatabaseMigrations().Single(s => IsMigration(s, TargetMigration));
            }
            else
            {
                // Up migration, go from start to target.
                start = migrator.GetDatabaseMigrations().FirstOrDefault();
                target = migrator.GetLocalMigrations().Single(s => IsMigration(s, TargetMigration));
            }

            string startName = start ?? migrator.GetDatabaseMigrations().FirstOrDefault();
            string scriptFileName = String.Format("{0}-{1}.sql", startName, target);
            if(File.Exists(scriptFileName)) {
                Log.Error("File already exists: {0}", scriptFileName);
                return;
            }

            // Generate script
            Log.Info("Scripting migration from {0} to {1}", startName, target);
            if (!WhatIf)
            {
                string script = scriptingMigrator.ScriptUpdate(start, target);

                // Write the script
                File.WriteAllText(scriptFileName, script);
            }
            Log.Info("Wrote script to {0}", scriptFileName);
        }

        private void RunMigrations(MigratorBase migrator)
        {
            // We only support UP right now.
            // Find the target migration and collect everything between the start and it
            var toApply = new List<string>();

            // TakeWhile won't work because it doesn't include the actual target :(
            foreach (var migration in migrator.GetPendingMigrations())
            {
                toApply.Add(migration);
                if (IsMigration(migration, TargetMigration))
                {
                    break;
                }
            }

            if (!toApply.Any(s => IsMigration(s, TargetMigration)))
            {
                Log.Error("{0} is not a pending migration. Only the UP direction can be run in this way. Use the -Sql option to script downwards migrations.", TargetMigration);
                return;
            }

            // We have a list of migrations to apply, apply them one-by-one
            foreach (var migration in toApply)
            {
                Log.Info("Applying {0}", migration);
                if (!WhatIf)
                {
                    migrator.Update(migration);
                }
            }
            Log.Info("All requested migrations applied");
        }

        private static bool IsMigration(string migrationId, string target)
        {
            // Get the shortname
            var match = MigrationIdRegex.Match(migrationId);
            if (!match.Success)
            {
                return String.Equals(migrationId, target, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var name = match.Groups["name"].Value;
                return String.Equals(name, target, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
