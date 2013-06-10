using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.SqlDac;

namespace NuGetGallery.Operations.Tasks
{
    [Command("exportdatabase", "Exports a sanitized copy of the database to blob storage", AltName = "xdb", MinArgs = 0, MaxArgs = 0)]
    public class ExportDatabaseTask : DatabaseTask
    {
        private IList<string> _unsanitizedUsers = new List<string>();

        [Option("Azure Storage Account in which the exported database should be placed", AltName = "s")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("URL of the SQL DAC endpoint to talk to", AltName = "dac")]
        public Uri SqlDacEndpoint { get; set; }

        [Option("Domain name to use for sanitized email addresses, username@[emaildomain]", AltName = "e")]
        public string EmailDomain { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            EmailDomain = String.IsNullOrEmpty(EmailDomain) ?
                "example.com" :
                EmailDomain;

            if (CurrentEnvironment != null)
            {
                if (DestinationStorage == null)
                {
                    DestinationStorage = CurrentEnvironment.DeveloperStorage;
                }
                if (SqlDacEndpoint == null)
                {
                    SqlDacEndpoint = CurrentEnvironment.SqlDac;
                }
            }

            ArgCheck.RequiredOrConfig(DestinationStorage, "DestinationStorage");
            ArgCheck.RequiredOrConfig(SqlDacEndpoint, "SqlDacEndpoint");
        }

        public override void ExecuteCommand()
        {
            // Phase 1. Create a copy of the database
            Log.Info("*** PHASE 1: Creating Database Copy ***");
            string name = "Export_" + Util.GetTimestamp();
            var backupJob = new BackupDatabaseTask()
            {
                ConnectionString = ConnectionString,
                BackupName = name,
                Force = true,
                WhatIf = WhatIf
            };
            backupJob.Execute();
            if (!WhatIf)
            {
                DatabaseBackupHelper.WaitForCompletion(backupJob);
            }

            // For extra safety, rewrite the connection string to target the backup
            ConnectionString = new SqlConnectionStringBuilder(ConnectionString.ConnectionString)
            {
                InitialCatalog = name
            };

            // Phase 2. Sanitize the database
            Log.Info("*** PHASE 2: Sanitizing the Copy ***");
            var sanitizeJob = new SanitizeDatabaseTask()
            {
                ConnectionString = ConnectionString,
                DatabaseName = name,
                EmailDomain = EmailDomain,
                WhatIf = WhatIf,
                Force = false
            };
            sanitizeJob.Execute();

            // Phase 3. Export the sanitized database to blob storage
            Log.Info("*** PHASE 3: Exporting to Blob Storage ***");
            string serverName = ConnectionString.DataSource;
            if (serverName.StartsWith("tcp:"))
            {
                serverName = serverName.Substring(4);
            }

            WASDImportExport.ImportExportHelper helper = new WASDImportExport.ImportExportHelper(Log)
            {
                EndPointUri = SqlDacEndpoint.AbsoluteUri,
                DatabaseName = name,
                ServerName = serverName,
                UserName = ConnectionString.UserID,
                Password = ConnectionString.Password,
                StorageKey = Convert.ToBase64String(DestinationStorage.Credentials.ExportKey())
            };
            
            // Prep the blob
            var client = DestinationStorage.CreateCloudBlobClient();
            var container = client.GetContainerReference("database-exports");
            container.CreateIfNotExists();
            var blob = container.GetBlockBlobReference(name + ".bacpac");

            // Export!
            string blobUrl = helper.DoExport(blob.Uri.AbsoluteUri, WhatIf);

            Log.Info("*** EXPORT COMPLETE ***");
            if (!String.IsNullOrEmpty(blobUrl))
            {
                Log.Info("Output: {0}", blobUrl);
            }
        }
    }
}
