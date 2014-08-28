using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Common;

namespace Catalog.Updater
{
    internal class Job
    {
        public static readonly int DefaultChecksumCollectorBatchSize = 2000;
        public static readonly int DefaultCatalogPageSize = 1000;

        private TraceLogger Logger;
        private Configuration Config;

        public SqlConnectionStringBuilder SourceDatabase { get; set; }
        public CloudStorageAccount CatalogStorage { get; set; }
        public string CatalogPath { get; set; }
        public int? ChecksumCollectorBatchSize { get; set; }
        public int? CatalogPageSize { get; set; }

        public Job() { }

        public bool Init(string[] args)
        {
            // Get the jobName and setup the logger. If this fails, don't catch it
            var jobName = this.GetType().ToString();
            Logger = new TraceLogger(jobName);

            // Initialize EventSources if any

            try
            {
                // Get the args. If you don't want args for the job, don't call the method below
                args = Configuration.GetJobArgs(args, jobName);

                Config = new Configuration();

                // Init member variables
                SourceDatabase = Config.SqlGallery;
                CatalogStorage = Config.StorageGallery;

                // Initialized successfully, return true
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(TraceLevel.Error, ex.ToString());
            }

            ShowHelp();
            return false;
        }

        public void Run()
        {
            Logger.Log(TraceLevel.Info, "Running...");
        }

        private void ShowHelp()
        {
            Logger.Log(TraceLevel.Info, "Help...");
            if(SourceDatabase == null)
            {
                Logger.Log(TraceLevel.Error, "SourceDatabase is invalid or not provided");
            }
            else if(CatalogStorage == null)
            {
                Logger.Log(TraceLevel.Error, "CatalogStorage  is invalid or not provided");
            }
            else
            {
                Logger.Log(TraceLevel.Error, "No help available");
            }
        }
    }
}
