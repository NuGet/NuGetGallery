using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Common;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Threading.Tasks;

namespace Catalog.Updater
{
    internal class Job : JobBase
    {
        private static readonly int DefaultChecksumCollectorBatchSize = 2000;
        private static readonly int DefaultCatalogPageSize = 1000;
        private JobEventSource JobEventSourceLog = JobEventSource.Log;

        private SqlConnectionStringBuilder SourceDatabase { get; set; }
        private CloudStorageAccount CatalogStorage { get; set; }
        private string CatalogPath { get; set; }
        private int? ChecksumCollectorBatchSize { get; set; }
        private int? CatalogPageSize { get; set; }

        public Job() : base(JobEventSource.Log) { }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                // Init member variables
                CatalogPath =
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.CatalogPath);
                SourceDatabase =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.SourceDatabase,
                            EnvironmentVariableKeys.SqlGallery));
                CatalogStorage =
                    CloudStorageAccount.Parse(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.CatalogStorage,
                            EnvironmentVariableKeys.StoragePrimary));
                ChecksumCollectorBatchSize =
                    JobConfigManager.TryGetIntArgument(jobArgsDictionary,
                    JobArgumentNames.ChecksumCollectorBatchSize);

                 // Initialized successfully, return true
                 return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            var collectorBatchSize = ChecksumCollectorBatchSize ?? DefaultChecksumCollectorBatchSize;
            var catalogPageSize = CatalogPageSize ?? DefaultCatalogPageSize;

            // Process:
            //  1. Load existing checksums file, if present
            //  2. Collect new checksums
            //  3. Process updates
            //  4. Collect new checksums
            //  5. Save existing checksums file

            // Set up helpers/contexts/etc.
            var catalogDirectory = StorageHelpers.GetBlobDirectory(CatalogStorage, CatalogPath);
            var checksums = new AzureStorageChecksumRecords(catalogDirectory.GetBlockBlobReference(AzureStorageChecksumRecords.DefaultChecksumFileName));
            var checksumCollector = new ChecksumCollector(collectorBatchSize, checksums);
            var http = CreateHttpClient();
            var indexBlob = catalogDirectory.GetBlockBlobReference("index.json");
            var storage = new AzureStorage(catalogDirectory);
            storage.Verbose = true;

            try
            {
                // Disposing of CatalogUpdater will dispose the HTTP client, 
                // so don't move this 'using' further in or we might dispose the HTTP client before we actually finish with it!
                using (var updater = new CatalogUpdater(new CatalogWriter(storage, new CatalogContext(), catalogPageSize), checksums, http))
                {
                    // 1. Load Checkums
                    JobEventSourceLog.LoadingChecksums(checksums.Uri.ToString());
                    await checksums.Load();
                    JobEventSourceLog.LoadedChecksums(checksums.Data.Count);

                    // 2. Collect new checksums
                    JobEventSourceLog.CollectingChecksums(catalogDirectory.Uri.ToString());
                    await checksumCollector.Run(http, indexBlob.Uri, checksums.Cursor);
                    JobEventSourceLog.CollectedChecksums(checksums.Data.Count);

                    // 3. Process updates
                    JobEventSourceLog.UpdatingCatalog();
                    await updater.Update(SourceDatabase.ConnectionString, indexBlob.Uri);
                    JobEventSourceLog.UpdatedCatalog();

                    // 4. Collect new checksums
                    JobEventSourceLog.CollectingChecksums(catalogDirectory.Uri.ToString());
                    await checksumCollector.Run(http, indexBlob.Uri, checksums.Cursor);
                    JobEventSourceLog.CollectedChecksums(checksums.Data.Count);

                    // 5. Save existing checksums file
                    JobEventSourceLog.SavingChecksums(checksums.Uri.ToString());
                    await checksums.Save();
                    JobEventSourceLog.SavedChecksums();
                }
            }
            catch (SqlException ex)
            {
                Trace.TraceError(ex.ToString());
                return false;
            }
            catch(StorageException ex)
            {
                Trace.TraceError(ex.ToString());
                return false;
            }

            return true;
        }

        private CollectorHttpClient CreateHttpClient()
        {
            var tracer = new TracingHttpHandler(new HttpClientHandler());
            tracer.OnSend += request =>
            {
                JobEventSourceLog.SendingHttpRequest(request.Method.ToString(), request.RequestUri.ToString());
            };
            tracer.OnException += (request, exception) =>
            {
                JobEventSourceLog.HttpException(request.RequestUri.ToString(), exception.ToString());
            };
            tracer.OnReceive += (request, response) =>
            {
                JobEventSourceLog.ReceivedHttpResponse((int)response.StatusCode, request.RequestUri.ToString());
            };
            return new CollectorHttpClient(tracer);
        }

        private void ShowHelp()
        {
            Trace.TraceInformation("\n\nHelp...");
            if(SourceDatabase == null)
            {
                Trace.TraceError("SourceDatabase is invalid or not provided");
            }
            else if(CatalogStorage == null)
            {
                Trace.TraceError("CatalogStorage is invalid or not provided");
            }
            else if(String.IsNullOrEmpty(CatalogPath))
            {
                Trace.TraceError("CatalogPath is invalid or not provided");
            }
            else
            {
                Trace.TraceError("No help available");
            }
        }
    }

    [EventSource(Name = "Outercurve-NuGet-Jobs-Catalog.Updater")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();
        private JobEventSource()
        { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.LoadingChecksums,
            Message = "Loading checksums from {0}")]
        public void LoadingChecksums(string uri) { WriteEvent(1, uri); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.LoadingChecksums,
            Message = "Loaded {0} checksums.")]
        public void LoadedChecksums(int count) { WriteEvent(2, count); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.HttpRequest,
            Message = "{0} {1}")]
        public void SendingHttpRequest(string method, string uri) { WriteEvent(3, method, uri); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.HttpRequest,
            Message = "{0} {1}")]
        public void ReceivedHttpResponse(int statusCode, string uri) { WriteEvent(4, statusCode, uri); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.HttpRequest,
            Message = "{0} {1}")]
        public void HttpException(string uri, string exception) { WriteEvent(5, uri, exception); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.CollectingChecksums,
            Message = "Collecting checksums from catalog at {0}")]
        public void CollectingChecksums(string uri) { WriteEvent(6, uri); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.CollectingChecksums,
            Message = "Collected new checksums. Total now {0}")]
        public void CollectedChecksums(int count) { WriteEvent(7, count); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.UpdatingCatalog,
            Message = "Running Catalog Updater")]
        public void UpdatingCatalog() { WriteEvent(8); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.UpdatingCatalog,
            Message = "Catalog Updater Completed")]
        public void UpdatedCatalog() { WriteEvent(9); }

        [Event(
            eventId: 10,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.SavingChecksums,
            Message = "Saving checksums to {0}")]
        public void SavingChecksums(string uri) { WriteEvent(10, uri); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.SavingChecksums,
            Message = "Saved checksums.")]
        public void SavedChecksums() { WriteEvent(11); }

        public static class Tasks
        {
            public const EventTask LoadingChecksums = (EventTask)0x1;
            public const EventTask HttpRequest = (EventTask)0x2;
            public const EventTask CollectingChecksums = (EventTask)0x2;
            public const EventTask UpdatingCatalog = (EventTask)0x3;
            public const EventTask SavingChecksums = (EventTask)0x4;
        }
    }
}
