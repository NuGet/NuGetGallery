using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ChecksumCollector : StoreCollector
    {
        public static readonly Uri[] Types = new Uri[] {
            new Uri("http://nuget.org/schema#Package")
        };

        private Dictionary<string, string> _collected = new Dictionary<string, string>();

        public TraceSource Trace { get; private set; }

        public ChecksumCollector(int batchSize) : base(batchSize, Types)
        {
            Trace = new TraceSource(typeof(ChecksumCollector).FullName);
        }

        public IDictionary<string, string> GetResults()
        {
            var ret = _collected;
            _collected = null;
            return ret;
        }

        public JObject Complete()
        {
            return JObject.FromObject(_collected);
        }

        protected override Task ProcessStore(TripleStore store)
        {
            Trace.TraceInformation("Processing batch {0}...", BatchCount);
            if (_collected == null)
            {
                throw new ObjectDisposedException("ChecksumCollector");
            }

            using (store)
            {
                SparqlResultSet results = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectChecksums.rq"));

                foreach (var result in results)
                {
                    string key = result["key"].ToString();
                    string checksum = result["checksum"].ToString();

                    _collected[key] = checksum;
                }
            }
            Trace.TraceInformation("Processed batch {0}...", BatchCount);
            return Task.FromResult(0);
        }
    }
}
