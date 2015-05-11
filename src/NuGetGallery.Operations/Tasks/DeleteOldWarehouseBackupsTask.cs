// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations
{
    [Command("purgewarehousebackups", "Deletes old database backups", AltName = "pwh")]
    public class DeleteOldWarehouseBackupsTask : WarehouseTask
    {
        public override void ExecuteCommand()
        {
            var dbServer = ConnectionString.DataSource;
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);

            Log.Trace("Deleting old warehouse backups for server '{0}':", dbServer);

            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                sqlConnection.Open();

                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    var dbs = dbExecutor.Query<Db>(
                        "SELECT name FROM sys.databases WHERE name LIKE 'WarehouseBackup_%' AND state = @state",
                        new { state = Util.OnlineState });

                    foreach (var db in dbs)
                    {
                        var timestamp = Util.GetDatabaseNameTimestamp(db);
                        var date = Util.GetDateTimeFromTimestamp(timestamp);
                        if (DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)) > date)
                            DeleteDatabaseBackup(db, dbExecutor);
                    }
                }
            }
        }

        private void DeleteDatabaseBackup(Db db, SqlExecutor dbExecutor)
        {
            if (!WhatIf)
            {
                dbExecutor.Execute(string.Format("DROP DATABASE {0}", db.Name));
            }
            Log.Info("Deleted database {0}.", db.Name);
        }
    }
}
