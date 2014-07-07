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
                string type = item.Value<string>("@type");
                var key = Int32.Parse(item.Value<string>("galleryKey"));
                if (String.Equals(type, "nuget:Package", StringComparison.Ordinal))
                {
                    var checksum = item.Value<string>("galleryChecksum");
                    var id = item.Value<string>("nuget:packageId");
                    var version = item.Value<string>("nuget:version");

                    Checksums.Data[key] = new JObject(
                        new JProperty("checksum", checksum),
                        new JProperty("id", id),
                        new JProperty("version", version));
                }
                else if (String.Equals(type, "nuget:DeletePackage", StringComparison.Ordinal))
                {
                    Checksums.Data.Remove(key);
                }
            }

            Trace.TraceInformation("Processed batch {0}...", BatchCount);
            return Task.FromResult(0);
        }
    }
}
