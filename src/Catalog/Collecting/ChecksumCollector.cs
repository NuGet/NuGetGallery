using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ChecksumCollector : BatchCollector
    {
        public ChecksumRecords Checksums { get; private set; }
        public TraceSource Trace { get; private set; }

        public ChecksumCollector(int batchSize, ChecksumRecords checksums) : base(batchSize)
        {
            Trace = new TraceSource(typeof(ChecksumCollector).FullName);
            Checksums = checksums;
        }

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            ChecksumCollectorEventSource.Log.Collecting(index.ToString(), ((DateTime)last).ToString("O"));
            
            // Run the collector
            var cursor = await base.Fetch(client, index, last);
            
            // Update the cursor
            Checksums.Cursor = cursor;

            ChecksumCollectorEventSource.Log.Collected();
            return cursor;
        }

        protected override Task<bool> ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            ChecksumCollectorEventSource.Log.ProcessingBatch(BatchCount, items.Count);
            
            foreach (var item in items)
            {
                string type = item.Value<string>("@type");
                string url = item.Value<string>("url");
                ChecksumCollectorEventSource.Log.CollectingItem(type, url);
                var key = Int32.Parse(item.Value<string>("galleryKey"));
                if (String.Equals(type, "nuget:Package", StringComparison.Ordinal))
                {
                    var checksum = item.Value<string>("galleryChecksum");
                    var id = item.Value<string>("nuget:id");
                    var version = item.Value<string>("nuget:version");

                    Checksums.Data[key] = new JObject(
                        new JProperty("checksum", checksum),
                        new JProperty("id", id),
                        new JProperty("version", version));
                }
                else if (String.Equals(type, "nuget:PackageDeletion", StringComparison.Ordinal))
                {
                    Checksums.Data.Remove(key);
                }
                ChecksumCollectorEventSource.Log.CollectedItem();
            }

            ChecksumCollectorEventSource.Log.ProcessedBatch();
            
            return Task.FromResult(true);
        }
    }

    [EventSource(Name="Outercurve-NuGet-Catalog-ChecksumCollector")]
    public class ChecksumCollectorEventSource : EventSource
    {
        public static readonly ChecksumCollectorEventSource Log = new ChecksumCollectorEventSource();
        private ChecksumCollectorEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.Collecting,
            Message = "Collecting catalog nodes from {0} since {1}")]
        public void Collecting(string indexUri, string cursor) { WriteEvent(1, indexUri, cursor); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.Collecting,
            Message = "Collection completed.")]
        public void Collected() { WriteEvent(2); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.ProcessingBatch,
            Message = "Processing Batch #{0}, containing {1} items.")]
        public void ProcessingBatch(int count, int items) { WriteEvent(3, count, items); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ProcessingBatch,
            Message = "Processed Batch.")]
        public void ProcessedBatch() { WriteEvent(4); }

        [Event(
            eventId: 5,
            Level = EventLevel.Verbose,
            Opcode = EventOpcode.Start,
            Task = Tasks.CollectingItem,
            Message = "Collecting {0} item {1}")]
        public void CollectingItem(string type, string uri) { WriteEvent(5, type, uri); }

        [Event(
            eventId: 6,
            Level = EventLevel.Verbose,
            Opcode = EventOpcode.Stop,
            Task = Tasks.CollectingItem,
            Message = "Collected Item")]
        public void CollectedItem() { WriteEvent(6); }

        public class Tasks
        {
            public const EventTask Collecting = (EventTask)0x1;
            public const EventTask ProcessingBatch = (EventTask)0x2;
            public const EventTask CollectingItem = (EventTask)0x3;
        }
    }
}
