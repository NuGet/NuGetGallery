using NuGet.Services.Metadata.Catalog.Helpers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class DistinctDependencyPackageIdCollector : StoreCollector
    {
        public DistinctDependencyPackageIdCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, new Uri[] { Schema.DataTypes.Package }, handlerFunc)
        {
            Result = new HashSet<string>();
        }

        public HashSet<string> Result
        {
            get; private set;
        }

        protected override async Task ProcessStore(TripleStore store)
        {
            string sparql = Utils.GetResource("sparql.SelectDistinctDependency.rq");

            foreach (SparqlResult row in SparqlHelpers.Select(store, sparql))
            {
                Result.Add(row["id"].ToString());
            }

            await Task.Run(() => { });
        }
    }
}
