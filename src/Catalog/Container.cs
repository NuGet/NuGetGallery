using Catalog.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog
{
    public class Container
    {
        List<Tuple<Uri, DateTime>> _items = new List<Tuple<Uri, DateTime>>();

        public List<Tuple<Uri, DateTime>> Items
        {
            get { return _items; }
        }

        public Uri Address
        {
            get;
            set;
        }

        public Uri Parent
        {
            get;
            set;
        }

        public async Task SaveTo(Storage storage)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode container = graph.CreateUriNode(Address);

            graph.Assert(container, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Container"));

            if (Parent != null)
            {
                graph.Assert(container, graph.CreateUriNode("nuget:parent"), graph.CreateUriNode(Parent));
            }

            INode itemPredicate = graph.CreateUriNode("nuget:item");
            INode publishedPredicate = graph.CreateUriNode("nuget:published");

            foreach (Tuple<Uri, DateTime> item in Items)
            {
                INode itemNode = graph.CreateUriNode(item.Item1);

                graph.Assert(container, itemPredicate, itemNode);
                graph.Assert(itemNode, publishedPredicate, graph.CreateLiteralNode(item.Item2.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#dateTime")));
            }

            JToken frame;
            using (JsonReader jsonReader = new JsonTextReader(new StreamReader(Utils.GetResourceStream("context.ContainerFrame.json"))))
            {
                frame = JObject.Load(jsonReader);
                frame["@type"] = "http://nuget.org/schema#Container";
            }

            string content = Utils.CreateJson(graph, frame);

            await storage.Save("application/json", Address, content);
        }
    }
}
