using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCollector : StoreCollector
    {
        Storage _storage;
        JObject _registrationFrame;

        public string GalleryBaseAddress { get; set; }
        public string ContentBaseAddress { get; set; }

        public RegistrationCollector(Storage storage, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _registrationFrame = JObject.Parse(Utils.GetResource("context.Resolver.json"));
            _registrationFrame["@type"] = "PackageRegistration";
            _storage = storage;

            GalleryBaseAddress = "http://tempuri.org";
            ContentBaseAddress = "http://tempuri.org";
        }

        protected override async Task ProcessStore(TripleStore store)
        {
            IDictionary<string, SortedSet<NuGetVersion>> packages = GetPackages(store);

            foreach (KeyValuePair<string, SortedSet<NuGetVersion>> registration in packages)
            {
                await ProcessRegistration(store, registration.Key, registration.Value);
            }
        }

        async Task ProcessRegistration(TripleStore store, string id, SortedSet<NuGetVersion> versions)
        {
            IList<Tuple<string, string, IList<KeyValuePair<Uri, IGraph>>>> ranges = new List<Tuple<string, string, IList<KeyValuePair<Uri, IGraph>>>>(); 

            foreach (IEnumerable<NuGetVersion> partition in Partition(versions, 10))
            {
                IList<KeyValuePair<Uri, IGraph>> packages = new List<KeyValuePair<Uri, IGraph>>();
                foreach (NuGetVersion version in partition)
                {
                    packages.Add(CreatePackageGraph(store, id, version));
                }
                ranges.Add(Tuple.Create(partition.First().ToString(), partition.Last().ToString(), packages));
            }

            KeyValuePair<Uri, IGraph> registration = CreateRegistrationGraph(id);

            Console.WriteLine(id);
            foreach (Tuple<string, string, IList<KeyValuePair<Uri, IGraph>>> range in ranges)
            {
                Uri rangeUri = new Uri(registration.Key.ToString() + "#range/" + range.Item1 + "/" + range.Item2);
                Uri rangePackagesUri = new Uri(registration.Key.ToString() + "#range/" + range.Item1 + "/" + range.Item2 + "/packages");

                INode rangeNode = registration.Value.CreateUriNode(rangeUri);
                INode rangePackagesNode = registration.Value.CreateUriNode(rangePackagesUri);

                registration.Value.Assert(registration.Value.CreateUriNode(registration.Key), registration.Value.CreateUriNode(Schema.Predicates.Range), rangeNode);
                registration.Value.Assert(rangeNode, registration.Value.CreateUriNode(Schema.Predicates.Low), registration.Value.CreateLiteralNode(range.Item1));
                registration.Value.Assert(rangeNode, registration.Value.CreateUriNode(Schema.Predicates.High), registration.Value.CreateLiteralNode(range.Item2));

                registration.Value.Assert(rangeNode, registration.Value.CreateUriNode(Schema.Predicates.RangePackages), rangePackagesNode);

                foreach (KeyValuePair<Uri, IGraph> package in range.Item3)
                {
                    registration.Value.Assert(rangePackagesNode, registration.Value.CreateUriNode(Schema.Predicates.Package), registration.Value.CreateUriNode(package.Key));
                    registration.Value.Merge(package.Value, true);
                }
            }

            string json = Utils.CreateJson(registration.Value, _registrationFrame);

            StorageContent content = new StringStorageContent(
                json, 
                contentType: "application/json", 
                cacheControl: "public, max-age=300, s-maxage=300");

            await _storage.Save(registration.Key, content);
        }

        KeyValuePair<Uri, IGraph> CreateRegistrationGraph(string id)
        {
            string baseAddress = _storage.BaseAddress.ToString();
            Uri resourceUri = new Uri(baseAddress + id + ".json");

            IGraph graph = new Graph();
            INode resource = graph.CreateUriNode(resourceUri);
            graph.Assert(resource, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PackageRegistration));
            graph.Assert(resource, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(id));
            return new KeyValuePair<Uri, IGraph>(resourceUri, graph);
        }

        KeyValuePair<Uri, IGraph> CreatePackageGraph(TripleStore store, string id, NuGetVersion version)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.ConstructPackageGraph.rq");

            string baseAddress = _storage.BaseAddress.ToString();

            sparql.SetLiteral("id", id);
            sparql.SetLiteral("version", version.ToString());
            sparql.SetLiteral("base", baseAddress);
            sparql.SetLiteral("extension", ".json");
            sparql.SetLiteral("galleryBase", GalleryBaseAddress);
            sparql.SetLiteral("contentBase", ContentBaseAddress);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            Uri resourceUri = ((IUriNode)graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Package)).First().Subject).Uri;

            Console.WriteLine("resource = {0}", resourceUri);

            return new KeyValuePair<Uri, IGraph>(resourceUri, graph);
        }

        public static IEnumerable<IEnumerable<T>> Partition<T>(IEnumerable<T> source, int size)
        {
            T[] array = null;
            int count = 0;
            foreach (T item in source)
            {
                if (array == null)
                {
                    array = new T[size];
                }
                array[count] = item;
                count++;
                if (count == size)
                {
                    yield return new ReadOnlyCollection<T>(array);
                    array = null;
                    count = 0;
                }
            }
            if (array != null)
            {
                Array.Resize(ref array, count);
                yield return new ReadOnlyCollection<T>(array);
            }
        }

        static IDictionary<string, SortedSet<NuGetVersion>> GetPackages(TripleStore store)
        {
            SparqlResultSet rows = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectPackages.rq"));

            IDictionary<string, SortedSet<NuGetVersion>> packages = new Dictionary<string, SortedSet<NuGetVersion>>();

            foreach (SparqlResult row in rows)
            {
                string id = row["id"].ToString();
                NuGetVersion version = NuGetVersion.Parse(row["version"].ToString());

                SortedSet<NuGetVersion> versions;
                if (!packages.TryGetValue(id, out versions))
                {
                    versions = new SortedSet<NuGetVersion>();
                    packages.Add(id, versions);
                }

                versions.Add(version);
            }

            return packages;
        }
    }
}
