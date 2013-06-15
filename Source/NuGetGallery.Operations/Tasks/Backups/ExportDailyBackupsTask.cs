using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations.Tasks
{
    [Command("exportdailybackups", "Exports the daily backup for each day to Blob storage", AltName = "xddb", IsSpecialPurpose = true)]
    public class ExportDailyBackupsTask : DatabaseTask
    {
        [Option("The storage account in which to place the backup", AltName = "s")]
        public CloudStorageAccount StorageAccount { get; set; }

        [Option("The URL of the SQL DAC Endpoint", AltName="dac")]
        public Uri SqlDacEndpoint { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (StorageAccount == null)
                {
                    StorageAccount = CurrentEnvironment.BackupStorage;
                }
                if (SqlDacEndpoint == null)
                {
                    SqlDacEndpoint = CurrentEnvironment.SqlDacEndpoint;
                }
            }

            ArgCheck.RequiredOrConfig(StorageAccount, "StorageAccount");
            ArgCheck.RequiredOrConfig(SqlDacEndpoint, "SqlDacEndpoint");
        }

        public override void ExecuteCommand()
        {
            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();

                // Snap the current date just in case we are running right on the cusp
                var today = DateTime.UtcNow;

                // Get the list of database backups
                var backups = db.Query<Database>(
                    "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                    new { state = Util.OnlineState })
                    .Select(d => new DatabaseBackup(Util.GetDatabaseServerName(ConnectionString), d.Name, d.State))
                    .OrderByDescending(b => b.Timestamp);

                // Grab end-of-day backups or sole backups from days before today
                var dailyBackups = backups
                    .GroupBy(b => b.Timestamp.Value.Date)
                    .Where(g => g.Key < today.Date)
                    .Select(g => g.OrderByDescending(b => b.Timestamp.Value).Last());

                // Start exporting them
                foreach (var dailyBackup in dailyBackups)
                {
                    if (dailyBackup.Timestamp.Value.TimeOfDay < new TimeSpan(23, 30, 00))
                    {
                        Log.Warn("Somehow, '{0}' is the only backup from {1}. Exporting it to be paranoid",
                            dailyBackup.DatabaseName,
                            dailyBackup.Timestamp.Value.Date.ToShortDateString());
                    }
                    Log.Info("Exporting '{0}'...", dailyBackup.DatabaseName);
                    (new ExportDatabaseTask()
                    {
                        ConnectionString = ConnectionString,
                        DestinationStorage = StorageAccount,
                        DatabaseName = dailyBackup.DatabaseName,
                        DestinationContainer = "database-backups",
                        SqlDacEndpoint = SqlDacEndpoint
                    }).Execute();
                }
            }
        }
    }
}
