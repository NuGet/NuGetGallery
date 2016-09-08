// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations.Tasks.Backups
{
    [Command("cleanwarehousebackups", "Clean and exports warehouse backups", AltName = "clwb")]
    public class CleanWarehouseBackupsTask : WarehouseTask
    {
        public override void ExecuteCommand()
        {
            WithMasterConnection((connection, db) =>
            {
                // Get the list of backups
                var backups = db.Query<Db>(
                    "SELECT name, state FROM sys.databases WHERE name LIKE 'WarehouseBackup_%'",
                    new { state = Util.OnlineState })
                    .Select(d => new OnlineDatabaseBackup(Util.GetDatabaseServerName(ConnectionString), d.Name, d.State))
                    .OrderByDescending(b => b.Timestamp)
                    .ToList();

                // Any currently copying are safe
                var keepers = new HashSet<string>();
                keepers.AddRange(backups.Where(b => b.State == Util.CopyingState).Select(b => b.DatabaseName));

                // The last online database is safe
                keepers.AddRange(backups
                    .Where(b => b.State == Util.OnlineState && b.Timestamp != null)
                    .OrderByDescending(d => d.Timestamp.Value)
                    .Select(b => b.DatabaseName)
                    .Take(1));

                // Figure out how many we're keeping
                Log.Info("Keeping the following Backups: {0}", String.Join(", ", keepers));

                // Done! Delete the non-keepers
                foreach (var backup in backups.Where(b => !keepers.Contains(b.DatabaseName)))
                {
                    if (!WhatIf)
                    {
                        db.Execute("DROP DATABASE " + backup.DatabaseName);
                    }
                    Log.Info("Deleted {0}", backup.DatabaseName);
                }
            });
        }
    }
}
