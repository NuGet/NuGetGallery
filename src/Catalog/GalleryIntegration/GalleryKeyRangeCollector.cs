using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryKeyRangeCollector : StoreCollector
    {
        Storage _storage;
        JObject _rangeFrame;

        public GalleryKeyRangeCollector(Storage storage, int batchSize)
            : base(batchSize, new Uri[] { Constants.Package, Constants.DeletePackage })
        {
            Options.InternUris = false;

            _rangeFrame = JObject.Parse(Utils.GetResource("context.Range.json"));
            _rangeFrame["@type"] = Constants.Range.ToString();
            _storage = storage;
        }

        protected override async Task ProcessStore(TripleStore store)
        {
            Uri baseAddress = _storage.ResolveUri("range/");
            
            int PageSize = 10000;

            HashSet<Uri> distinctResourceUri = new HashSet<Uri>();
            IDictionary<Uri, IGraph> adds = new Dictionary<Uri, IGraph>();
            IDictionary<Uri, IGraph> deletes = new Dictionary<Uri, IGraph>();

            SparqlResultSet rangeUpdates = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectRangeUpdates.rq"));
            foreach (SparqlResult row in rangeUpdates)
            {
                string type = row["type"].ToString();
                int key = int.Parse(row["key"].ToString());

                int pageNumber = key / PageSize;

                Uri resourceUri = new Uri(baseAddress, String.Format("page{0}.json", pageNumber));

                distinctResourceUri.Add(resourceUri);

                if (type == Constants.Package.ToString())
                {
                    IGraph g;
                    if (!adds.TryGetValue(resourceUri, out g))
                    {
                        g = new Graph();
                        g.Assert(g.CreateUriNode(resourceUri), g.CreateUriNode(Constants.RdfType), g.CreateUriNode(Constants.Range));
                        adds.Add(resourceUri, g);
                    }
                    g.Assert(g.CreateUriNode(resourceUri), g.CreateUriNode(new Uri("http://nuget.org/gallery#key")), g.CreateLiteralNode(key.ToString(), Constants.Integer));
                }

                if (type == Constants.DeletePackage.ToString())
                {
                    IGraph g;
                    if (!deletes.TryGetValue(resourceUri, out g))
                    {
                        g = new Graph();
                        deletes.Add(resourceUri, g);
                    }
                    g.Assert(g.CreateUriNode(resourceUri), g.CreateUriNode(new Uri("http://nuget.org/gallery#key")), g.CreateLiteralNode(key.ToString(), Constants.Integer));
                }
            }

            await UpdateRangePages(distinctResourceUri, adds, deletes);
        }

        async Task UpdateRangePages(HashSet<Uri> distinctResourceUri, IDictionary<Uri, IGraph> adds, IDictionary<Uri, IGraph> deletes)
        {
            Uri baseAddress = _storage.ResolveUri("range/");
            
            foreach (Uri resourceUri in distinctResourceUri)
            {
                IGraph g = null;
                adds.TryGetValue(resourceUri, out g);

                string existingJson = await _storage.LoadString(resourceUri);
                if (existingJson != null)
                {
                    IGraph existingGraph = Utils.CreateGraph(existingJson);

                    if (g == null)
                    {
                        g = existingGraph;
                    }
                    else
                    {
                        g.Merge(existingGraph);
                    }
                }

                IGraph d;
                if (deletes.TryGetValue(resourceUri, out d))
                {
                    foreach (Triple t in d.Triples)
                    {
                        g.Retract(t);
                    }
                }

                StorageContent content = new StringStorageContent(Utils.CreateJson(g, _rangeFrame), "application/json");
                await _storage.Save(resourceUri, content);
            }

            Uri rangeIndexUri = new Uri(baseAddress, "index.json");
            string rangeIndexJson = await _storage.LoadString(rangeIndexUri);

            IGraph indexGraph = (rangeIndexJson != null) ? Utils.CreateGraph(rangeIndexJson) : new Graph();

            INode subject = indexGraph.CreateUriNode(rangeIndexUri);
            INode predicate = indexGraph.CreateUriNode(new Uri("http://nuget.org/gallery#range"));

            foreach (Uri resourceUri in distinctResourceUri)
            {
                indexGraph.Assert(subject, predicate, indexGraph.CreateUriNode(resourceUri));
            }

            indexGraph.Assert(indexGraph.CreateUriNode(rangeIndexUri), indexGraph.CreateUriNode(Constants.RdfType), indexGraph.CreateUriNode(Constants.Range));

            StorageContent indexContent = new StringStorageContent(Utils.CreateJson(indexGraph, _rangeFrame), "application/json");
            await _storage.Save(rangeIndexUri, indexContent);
        }
    }
}
