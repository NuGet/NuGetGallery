using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class CatalogRoot : CatalogContainer
    {
        IDictionary<Uri, Tuple<Uri, IGraph, DateTime, int?>> _items;
        IDictionary<string, string> _commitUserData;
        string _baseAddress;
        int _nextPageNumber;

        Uri _latestUri;
        int _latestCount;

        public CatalogRoot(Uri root, string content)
            : base(root)
        {
            _items = new Dictionary<Uri, Tuple<Uri, IGraph, DateTime, int?>>();

            _nextPageNumber = 0;
            if (content != null)
            {
                Load(_items, content);

                Tuple<int, Uri, int> latest = ExtractLatest();
                _nextPageNumber = latest.Item1 + 1;
                _latestUri = latest.Item2;
                _latestCount = latest.Item3;
            }

            string s = root.ToString();
            _baseAddress = s.Substring(0, s.LastIndexOf('/') + 1);
        }

        public Uri AddNextPage(DateTime timeStamp, int count)
        {
            Uri nextPageAddress = new Uri(_baseAddress + string.Format("page{0}.json", _nextPageNumber++));
            _items.Add(nextPageAddress, new Tuple<Uri, IGraph, DateTime, int?>(Constants.CatalogPage, null, timeStamp, count));
            return nextPageAddress;
        }

        public Tuple<Uri, int> GetLatestPage()
        {
            return _latestUri == null ? null : new Tuple<Uri, int>(_latestUri, _latestCount);
        }

        public void UpdatePage(Uri pageUri, DateTime timeStamp, int count)
        {
            _items[pageUri] = new Tuple<Uri, IGraph, DateTime, int?>(Constants.CatalogPage, null, timeStamp, count);
        }

        public void SetCommitUserData(IDictionary<string, string> commitUserData)
        {
            _commitUserData = commitUserData;
        }

        public static IDictionary<string, string> GetCommitUserData(Uri resourceUri, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));

            IDictionary<string, string> commitUserData = null;

            foreach (Triple commitUserDataSetTriples in graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(resourceUri), graph.CreateUriNode("catalog:commitUserData")))
            {
                foreach (Triple commitUserDataTriple in graph.GetTriplesWithSubject(commitUserDataSetTriples.Object))
                {
                    string predicate = commitUserDataTriple.Predicate.ToString();
                    string key = predicate.Substring(predicate.LastIndexOf("$") + 1);
                    string value = commitUserDataTriple.Object.ToString();

                    if (commitUserData == null)
                    {
                        commitUserData = new Dictionary<string, string>();
                    }

                    commitUserData.Add(key, value);
                }
            }

            return commitUserData;
        }

        public static DateTime GetLastCommitTimeStamp(Uri resourceUri, string content)
        {
            IGraph graph = Utils.CreateGraph(content);
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));
            Triple timeStampTriple = graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(resourceUri), graph.CreateUriNode("catalog:timeStamp")).First();
            return DateTime.Parse(((ILiteralNode)timeStampTriple.Object).Value);
        }

        public static int GetCount(Uri resourceUri, string content)
        {
            IGraph graph = Utils.CreateGraph(content);
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));

            int total = 0;

            foreach (Triple itemTriples in graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(resourceUri), graph.CreateUriNode("catalog:item")))
            {
                foreach (Triple countTriple in graph.GetTriplesWithSubjectPredicate(itemTriples.Object, graph.CreateUriNode("catalog:count")))
                {
                    int count = int.Parse(countTriple.Object.ToString());
                    total += count;
                }
            }

            return total;
        }

        protected override void AddCustomContent(INode resource, IGraph graph)
        {
            if (_commitUserData != null)
            {
                string baseAddress = ((IUriNode)resource).Uri.ToString();
                INode commitUserData = graph.CreateUriNode(new Uri(baseAddress + "#commitUserData"));
                graph.Assert(resource, graph.CreateUriNode("catalog:commitUserData"), commitUserData);
                foreach (KeyValuePair<string, string> item in _commitUserData)
                {
                    graph.Assert(commitUserData, graph.CreateUriNode("catalog:property$" + item.Key), graph.CreateLiteralNode(item.Value));
                }
            }
        }

        protected override Uri GetContainerType()
        {
            return Constants.CatalogRoot;
        }

        protected override IDictionary<Uri, Tuple<Uri, IGraph, DateTime, int?>> GetItems()
        {
            return _items;
        }

        Tuple<int, Uri, int> ExtractLatest()
        {
            int maxPageNumber = -1;
            Uri latestUri = null;
            int latestCount = 0;

            foreach (KeyValuePair<Uri, Tuple<Uri, IGraph, DateTime, int?>> item in _items)
            {
                string s = item.Key.ToString();
                s = s.Substring(s.LastIndexOf('/') + 5);
                s = s.Substring(0, s.Length - 5);

                int pageNumber = int.Parse(s);
                if (pageNumber > maxPageNumber)
                {
                    maxPageNumber = pageNumber;
                    latestUri = item.Key;
                    latestCount = item.Value.Item4.Value;
                }
            }

            return new Tuple<int, Uri, int>(maxPageNumber, latestUri, latestCount);
        }
    }
}
