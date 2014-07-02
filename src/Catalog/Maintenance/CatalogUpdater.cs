using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        
        private CatalogWriter _writer;
        private ChecksumRecords _checksums;
        private CollectorHttpClient _client;

        public int DatabaseChecksumBatchSize { get; set; }
        public int CatalogAddBatchSize { get; set; }

        public TraceSource Trace { get; private set; }

        public CatalogUpdater(CatalogWriter writer, ChecksumRecords checksums, CollectorHttpClient client)
        {
            DatabaseChecksumBatchSize = DefaultDatabaseChecksumBatchSize;
            CatalogAddBatchSize = DefaultCatalogAddBatchSize;

            _writer = writer;
            _checksums = checksums;
            _client = client;

            Trace = new TraceSource(typeof(CatalogUpdater).FullName);
        }

        public async Task Update(string packageDatabaseConnectionString, Uri catalogIndexUrl, Uri galleryBaseUrl, Uri downloadBaseUrl)
        {
            // Collect Memory Usage Snapshot
            Trace.TraceInformation("Memory Usage {0:0.00}MB", GetMemoryInMB());

            // Load checksum data
            await _checksums.Load();
            Trace.TraceInformation("Loaded {0} checksums from catalog...", _checksums.Data.Count);

            // Collect Memory Usage of Catalog Checksum Data
            Trace.TraceInformation("Memory Usage {0:0.00}MB", GetMemoryInMB());

            // Collect Database Checksums
            var databaseChecksums = new Dictionary<int, string>(_checksums.Data.Count);
            int lastKey = 0;
            int batchSize = DatabaseChecksumBatchSize; // Capture the value to prevent the caller from tinkering with it :)
            while (true)
            {
                var range = await GalleryExport.FetchChecksums(packageDatabaseConnectionString, lastKey, batchSize);
                foreach (var pair in range)
                {
                    databaseChecksums[pair.Key] = pair.Value;
                }
                if (range.Count < batchSize)
                {
                    break;
                }
                lastKey = range.Max(p => p.Key);
                Trace.TraceInformation("Loaded {0} total checksums from database...", databaseChecksums.Count);
            }
            Trace.TraceInformation("Loaded all checksums from database.");
            Trace.TraceInformation("Memory Usage {0:0.00}MB", GetMemoryInMB());

            // Diff the checksums
            var diffs = GalleryExport.CompareChecksums(_checksums.Data, databaseChecksums).ToList();
            Trace.TraceInformation("Found {0} differences", diffs.Count);

            // Update the catalog
            var batcher = new GalleryExportBatcher(CatalogAddBatchSize, _writer, galleryBaseUrl, downloadBaseUrl);
            Trace.TraceInformation("Adding new data to catalog");
            foreach (var diff in diffs)
            {
                if (diff.Result == ComparisonResult.DifferentInCatalog || diff.Result == ComparisonResult.PresentInDatabaseOnly)
                {
                    Trace.TraceInformation("Updating package {0} from database ...", diff.Key);
                    GalleryExport.WritePackage(packageDatabaseConnectionString, diff.Key, batcher).Wait();
                }
                else
                {
                    // Write a deletion of this package
                    Console.WriteLine("Package {0} {1} was removed from database. Adding a deletion to the catalog.", diff.Id, diff.Version);

                    batcher.Add(new DeletePackageCatalogItem(diff.Id, diff.Version, diff.Key.ToString())).Wait();
                }
            }
            await batcher.Complete();
            await _writer.Commit();

            // Set up to forward trace events from the collector.
            var collector = new ChecksumCollector(1000, _checksums);
            collector.Trace.Listeners.AddRange(Trace.Listeners);
            collector.Trace.Switch.Level = Trace.Switch.Level;

            // Update checksums
            var timestamp = DateTime.UtcNow;
            Trace.TraceInformation("Collecting new checksums since: {0} UTC", _checksums.TimestampUtc);
            await collector.Run(_client, catalogIndexUrl, _checksums.TimestampUtc);

            // Update the timestamp and save the checksum data
            _checksums.TimestampUtc = timestamp;
            await _checksums.Save();
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
}
