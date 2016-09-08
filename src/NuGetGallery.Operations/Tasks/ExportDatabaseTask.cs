// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

namespace NuGetGallery.Operations
{
    [Command("exportdatabase", "Exports a copy of the database to blob storage", AltName = "xdb", MinArgs = 0, MaxArgs = 0)]
    public class ExportDatabaseTask : DatabaseTask
    {
        [Option("Azure Storage Account in which the exported database should be placed", AltName = "s")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("Blob container in which the backup should be placed", AltName = "c")]
        public string DestinationContainer { get; set; }

        [Option("URL of the SQL DAC endpoint to talk to", AltName = "dac")]
        public Uri SqlDacEndpoint { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (DestinationStorage == null)
                {
                    DestinationStorage = CurrentEnvironment.BackupStorage;
                }
                if (SqlDacEndpoint == null)
                {
                    SqlDacEndpoint = CurrentEnvironment.SqlDacEndpoint;
                }
            }

            ArgCheck.RequiredOrConfig(DestinationStorage, "DestinationStorage");
            ArgCheck.RequiredOrConfig(SqlDacEndpoint, "SqlDacEndpoint");
            ArgCheck.Required(DestinationContainer, "DestinationContainer");
        }

        public override void ExecuteCommand()
        {
            Log.Info("Exporting {0} on {1} to {2}", ConnectionString.InitialCatalog, Util.GetDatabaseServerName(ConnectionString), DestinationStorage.Credentials.AccountName);

            string serverName = ConnectionString.DataSource;
            if (serverName.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                serverName = serverName.Substring(4);
            }

            WASDImportExport.ImportExportHelper helper = new WASDImportExport.ImportExportHelper(Log)
            {
                EndPointUri = SqlDacEndpoint.AbsoluteUri,
                DatabaseName = ConnectionString.InitialCatalog,
                ServerName = serverName,
                UserName = ConnectionString.UserID,
                Password = ConnectionString.Password,
                StorageKey = Convert.ToBase64String(DestinationStorage.Credentials.ExportKey())
            };

            // Prep the blob
            string blobUrl = null;
            if (!WhatIf)
            {
                var client = DestinationStorage.CreateCloudBlobClient();
                var container = client.GetContainerReference(DestinationContainer);
                container.CreateIfNotExists();
                var blob = container.GetBlockBlobReference(ConnectionString.InitialCatalog + ".bacpac");
                if (blob.Exists())
                {
                    Log.Info("Skipping export of {0} because the blob already exists", blob.Name);
                }
                else
                {
                    Log.Info("Starting export to {0}", blob.Uri.AbsoluteUri);

                    // Export!
                    blobUrl = helper.DoExport(blob.Uri.AbsoluteUri, WhatIf);
                }
            }

            Log.Info("Exported to {0}", blobUrl);
        }
    }
}
