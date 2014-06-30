using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ChecksumCollector : BatchCollector
    {
        public static readonly Uri[] Types = new Uri[] {
            new Uri("http://nuget.org/schema#Package")
        };

        public ChecksumRecords Checksums { get; private set; }
        public TraceSource Trace { get; private set; }

        public ChecksumCollector(int batchSize, ChecksumRecords checksums) : base(batchSize)
        {
            Trace = new TraceSource(typeof(ChecksumCollector).FullName);
            Checksums = checksums;
        }

        protected override Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            Trace.TraceInformation("Processing batch {0}...", BatchCount);
            
            foreach (var item in items)
            {
                var key = Int32.Parse(item.Value<string>("gallery:key"));
                var checksum = item.Value<string>("gallery:checksum");

                Checksums.Data[key] = checksum;
            }

            Trace.TraceInformation("Processed batch {0}...", BatchCount);
            return Task.FromResult(0);
        }
    }
}
