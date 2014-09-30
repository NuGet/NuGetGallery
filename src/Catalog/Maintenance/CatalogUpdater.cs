using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class CatalogUpdater : IDisposable
    {
        public static readonly int DefaultDatabaseChecksumBatchSize = 50000;
        public static readonly int DefaultCatalogAddBatchSize = 2000;

        private AppendOnlyCatalogWriter _writer;
        private ChecksumRecords _checksums;
        private CollectorHttpClient _client;
        
        public int DatabaseChecksumBatchSize { get; set; }
        public int CatalogAddBatchSize { get; set; }

        public CatalogUpdater(AppendOnlyCatalogWriter writer, ChecksumRecords checksums, CollectorHttpClient client)
        {
            DatabaseChecksumBatchSize = DefaultDatabaseChecksumBatchSize;
            CatalogAddBatchSize = DefaultCatalogAddBatchSize;

            _writer = writer;
            _checksums = checksums;
            _client = client;
        }

        public async Task Update(string sqlConnectionString, Uri catalogIndexUrl)
        {
            // Collect Database Checksums
            CatalogUpdaterEventSource.Log.CollectingDatabaseChecksums();
            var databaseChecksums = new Dictionary<int, string>(_checksums.Data.Count);
            int lastKey = 0;
            int batchSize = DatabaseChecksumBatchSize; // Capture the value to prevent the caller from tinkering with it :)
            while (true)
            {
                var range = await GalleryExport.FetchChecksums(sqlConnectionString, lastKey, batchSize);
                foreach (var pair in range)
                {
                    databaseChecksums[pair.Key] = pair.Value;
                }
                if (range.Count < batchSize)
                {
                    break;
                }
                lastKey = range.Max(p => p.Key);
                CatalogUpdaterEventSource.Log.CollectedDatabaseChecksumBatch(databaseChecksums.Count);
            }
            CatalogUpdaterEventSource.Log.CollectedDatabaseChecksums(databaseChecksums.Count);

            // Diff the checksums
            CatalogUpdaterEventSource.Log.ComparingChecksums();
            var diffs = GalleryExport.CompareChecksums(_checksums.Data, databaseChecksums).ToList();
            CatalogUpdaterEventSource.Log.ComparedChecksums(diffs.Count);

            // Update the catalog
            CatalogUpdaterEventSource.Log.UpdatingCatalog();
            var batcher = new GalleryExportBatcher(CatalogAddBatchSize, _writer);
            foreach (var diff in diffs)
            {
                // Temporarily ignoring updates to avoid the resolver collector going a little wonky
                //  diff.Result == ComparisonResult.DifferentInCatalog ||
                if (diff.Result == ComparisonResult.PresentInDatabaseOnly)
                {
                    CatalogUpdaterEventSource.Log.WritingItem(
                        "Update", diff.Key, diff.Id, diff.Version);
                    await GalleryExport.WritePackage(sqlConnectionString, diff.Key, batcher);
                }
                // Also disabling deletes due to some edge cases.
                //else if(diff.Result == ComparisonResult.PresentInCatalogOnly)
                //{
                //    CatalogUpdaterEventSource.Log.WritingItem(
                //        "Delete", diff.Key, diff.Id, diff.Version);

                //    await batcher.Add(new DeletePackageCatalogItem(diff.Id, diff.Version, diff.Key.ToString()));
                //}
                CatalogUpdaterEventSource.Log.WroteItem();
            }
            await batcher.Complete();
            await _writer.Commit();
            CatalogUpdaterEventSource.Log.UpdatedCatalog();
        }

        public void Dispose()
        {
            _writer.Dispose();
            _client.Dispose();
        }

        private double GetMemoryInMB()
        {
            return (double)GC.GetTotalMemory(forceFullCollection: true) / (1024 * 1024);
        }
    }

    [EventSource(Name = "Outercurve-NuGet-Catalog-CatalogUpdater")]
    public class CatalogUpdaterEventSource : EventSource
    {
        public static readonly CatalogUpdaterEventSource Log = new CatalogUpdaterEventSource();
        private CatalogUpdaterEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.CollectDatabaseChecksums,
            Message = "Collecting checksums from database")]
        public void CollectingDatabaseChecksums() { WriteEvent(1); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.CollectDatabaseChecksums,
            Message = "Collected {0} checksums from database")]
        public void CollectedDatabaseChecksums(int count) { WriteEvent(2, count); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.CompareChecksums,
            Message = "Comparing checksums from database to catalog...")]
        public void ComparingChecksums() { WriteEvent(3); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.CompareChecksums,
            Message = "Compared checksums from database to catalog. Found {0} differences.")]
        public void ComparedChecksums(int count) { WriteEvent(4, count); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.UpdateCatalog,
            Message = "Updating Catalog...")]
        public void UpdatingCatalog() { WriteEvent(5); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.UpdateCatalog,
            Message = "Updated Catalog.")]
        public void UpdatedCatalog() { WriteEvent(6); }

        [Event(
            eventId: 7,
            Level = EventLevel.Verbose,
            Opcode = EventOpcode.Start,
            Task = Tasks.WriteItem,
            Message = "Writing {0} of record #{1} ({2} v{3}) to catalog.")]
        public void WritingItem(string type, int key, string id, string version) { WriteEvent(7, type, key, id, version); }

        [Event(
            eventId: 8,
            Level = EventLevel.Verbose,
            Opcode = EventOpcode.Stop,
            Task = Tasks.WriteItem,
            Message = "Wrote item to catalog.")]
        public void WroteItem() { WriteEvent(8); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Task = Tasks.CollectDatabaseChecksums,
            Message = "Collected {0} checksums from database so far...")]
        public void CollectedDatabaseChecksumBatch(int count) { WriteEvent(9, count); }

        public class Tasks
        {
            public const EventTask CollectDatabaseChecksums = (EventTask)0x1;
            public const EventTask CompareChecksums = (EventTask)0x2;
            public const EventTask UpdateCatalog = (EventTask)0x3;
            public const EventTask WriteItem = (EventTask)0x4;
        }
    }
}
