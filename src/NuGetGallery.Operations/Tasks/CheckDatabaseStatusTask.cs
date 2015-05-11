// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("checkdatabase", "Checks the status of the database", AltName = "chkdb", MaxArgs = 0)]
    public class CheckDatabaseStatusTask: DatabaseTask
    {
        [Option("Force a backup, even if there is one less than 24 hours old", AltName="db")]
        public string BackupName { get; set; }

        public int State { get; private set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(BackupName, "BackupName");
        }

        public override void ExecuteCommand()
        {
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);

            using (SqlConnection connection = new SqlConnection(masterConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("SELECT [state] FROM sys.databases WHERE [name] = @BackupName", connection);
                command.Parameters.AddWithValue("BackupName", BackupName);

                SqlDataReader reader = command.ExecuteReader();

                int count = 0;

                while (reader.Read())
                {
                    State = int.Parse(reader.GetValue(0).ToString());

                    count++;
                }

                if (count < 1)
                {
                    State = -1; // Not present.
                }
                if (count > 1)
                {
                    throw new InvalidOperationException("Please provide a specific database name");
                }

                Log.Info("'{0}' State: {1}", BackupName, State);
            }
        }
    }
}
