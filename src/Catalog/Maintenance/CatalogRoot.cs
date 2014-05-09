using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace Catalog.Maintenance
{
    class CatalogRoot : CatalogContainer
    {
        List<Tuple<Uri, DateTime>> _items;
        string _baseAddress;
        int _nextPageNumber;

        public CatalogRoot(Uri root, string content)
            : base(root)
        {
            _items = new List<Tuple<Uri, DateTime>>();

            _nextPageNumber = 0;
            if (content != null)
            {
                int maxPageNumber = ExtractCurrentItems(_items, content);
                _nextPageNumber = maxPageNumber + 1;
            }

            string s = root.ToString();
            _baseAddress = s.Substring(0, s.LastIndexOf('/') + 1);
        }

        public Uri GetNextPageAddress(DateTime timeStamp)
        {
            Uri nextPageAddress = new Uri(_baseAddress + string.Format("page{0}.json", _nextPageNumber++));
            _items.Add(new Tuple<Uri, DateTime>(nextPageAddress, timeStamp));
            return nextPageAddress;
        }

        protected override IEnumerable<Tuple<Uri, DateTime>> GetItems()
        {
            return _items;
        }

        static int ExtractCurrentItems(List<Tuple<Uri, DateTime>> _items, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode itemNode = graph.CreateUriNode("nuget:item");
            INode publishedNode = graph.CreateUriNode("nuget:published");

            int maxPageNumber = 0;

            foreach (Triple itemTriple in graph.GetTriplesWithPredicate(itemNode))
            {
                Triple publishedTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, publishedNode).First();

                Uri itemUri = ((IUriNode)itemTriple.Object).Uri;
                DateTime published = DateTime.Parse(((ILiteralNode)publishedTriple.Object).Value);

                _items.Add(new Tuple<Uri, DateTime>(itemUri, published));

                string s = itemUri.ToString();
                s = s.Substring(s.LastIndexOf('/') + 5);
                s = s.Substring(0, s.Length - 5);

                int pageNumber = int.Parse(s);
                if (pageNumber > maxPageNumber)
                {
                    maxPageNumber = pageNumber;
                }
            }

            return maxPageNumber;
        }
    }
}
