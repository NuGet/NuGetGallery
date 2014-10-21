using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public static class PackageCatalog
    {
        public static IGraph CreateCommitMetadata(Uri indexUri, DateTime? lastCreated, DateTime? lastEdited)
        {
            IGraph graph = new Graph();

            if (lastCreated != null)
            {
                graph.Assert(graph.CreateUriNode(indexUri), graph.CreateUriNode(Schema.Predicates.LastCreated), graph.CreateLiteralNode(lastCreated.Value.ToString("O"), Schema.DataTypes.DateTime));
            }
            if (lastEdited != null)
            {
                graph.Assert(graph.CreateUriNode(indexUri), graph.CreateUriNode(Schema.Predicates.LastEdited), graph.CreateLiteralNode(lastEdited.Value.ToString("O"), Schema.DataTypes.DateTime));
            }

            return graph;
        }

        public static async Task<Tuple<DateTime?, DateTime?>> ReadCommitMetadata(CatalogWriterBase writer)
        {
            DateTime? lastCreated = null;
            DateTime? lastEdited = null;

            string json = await writer.Storage.LoadString(writer.RootUri);

            if (json != null)
            {
                JObject obj;

                using (JsonReader jsonReader = new JsonTextReader(new StringReader(json)))
                {
                    jsonReader.DateParseHandling = DateParseHandling.None;
                    obj = JObject.Load(jsonReader);
                }

                JToken t1;
                if (obj.TryGetValue("nuget:lastCreated", out t1))
                {
                    lastCreated = DateTime.Parse(t1.ToString(), null, DateTimeStyles.RoundtripKind);
                }

                JToken t2;
                if (obj.TryGetValue("nuget:lastEdited", out t2))
                {
                    lastCreated = DateTime.Parse(t2.ToString(), null, DateTimeStyles.RoundtripKind);
                }
            }

            return Tuple.Create(lastCreated, lastEdited);
        }
    }
}
