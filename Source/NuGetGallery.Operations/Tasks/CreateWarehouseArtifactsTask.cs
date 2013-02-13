using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;

namespace NuGetGallery.Operations.Tasks
{
    [Command("createwarehousedatabase", "Create warehouse artifacts", AltName = "cwdb")]
    public class CreateWarehouseArtifactsTask : OpsTask
    {
        [Option("Connection string to the warehouse database", AltName = "wdb")]
        public string WarehouseConnectionString { get; set; }

        [Option("Force recreation of the database artifacts", AltName = "f")]
        public bool Force { get; set; }

        public CreateWarehouseArtifactsTask()
        {
            WarehouseConnectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
        }

        public override void ExecuteCommand()
        {
            Log.Info("Create warehouse artifacts");

            AddTablesAndProcs();
            PrePopulateDimensions();

            Log.Info("Warehouse artifacts successfully created");
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.RequiredOrEnv(WarehouseConnectionString, "WarehouseConnectionString", "NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
        }

        void AddTablesAndProcs()
        {
            Log.Info("Adding Tables and Stored Procedures");

            if (Force)
            {
                Log.Info("Dropping any existing tables.");

                ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsDropTables.sql");
            }

            Log.Info("Creating tables.");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsCreateTables.sql");

            Log.Info("Creating stored functions and procedures.");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsFuncs_UserAgent.sql");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsProcs_AddDownloadFact.sql");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsProcs_ConfirmPackageExported.sql");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsProcs_GetLastOriginalKey.sql");
            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.NuGetDownloadsProcs_GetPackagesForExport.sql");
        }

        void PrePopulateDimensions()
        {
            Log.Info("Pre-populating Dimensions");

            ExecuteSqlBatch("NuGetGallery.Operations.Scripts.PopulateDimensions.sql");
        }

        void ExecuteSqlBatch(string name)
        {
            IEnumerable<string> batches = ResourceHelper.GetBatchesFromSqlFile(name);
            foreach (string batch in batches)
            {
                SqlHelper.ExecuteBatch(WarehouseConnectionString, batch);
            }
        }
    }
}

