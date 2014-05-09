using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace Catalog.Maintenance
{
    public class CatalogRoot : CatalogContainer
    {
        List<Tuple<Uri, DateTime, int?>> _items;
        string _baseAddress;
        int _nextPageNumber;

        public CatalogRoot(Uri root, string content)
            : base(root)
        {
            _items = new List<Tuple<Uri, DateTime, int?>>();

            _nextPageNumber = 0;
            if (content != null)
            {
                int maxPageNumber = ExtractCurrentItems(_items, content);
                _nextPageNumber = maxPageNumber + 1;
            }

            string s = root.ToString();
            _baseAddress = s.Substring(0, s.LastIndexOf('/') + 1);
        }

        public Uri GetNextPageAddress(DateTime timeStamp, int count)
        {
            Uri nextPageAddress = new Uri(_baseAddress + string.Format("page{0}.json", _nextPageNumber++));
            _items.Add(new Tuple<Uri, DateTime, int?>(nextPageAddress, timeStamp, count));
            return nextPageAddress;
        }

        protected override IEnumerable<Tuple<Uri, DateTime, int?>> GetItems()
        {
            return _items;
        }

        static int ExtractCurrentItems(List<Tuple<Uri, DateTime, int?>> _items, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode itemPredicate = graph.CreateUriNode("nuget:item");
            INode publishedPredicate = graph.CreateUriNode("nuget:published");
            INode countPredicate = graph.CreateUriNode("nuget:count");

            int maxPageNumber = 0;

            foreach (Triple itemTriple in graph.GetTriplesWithPredicate(itemPredicate))
            {
                Triple publishedTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, publishedPredicate).First();
                Triple countTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, countPredicate).First();

                Uri itemUri = ((IUriNode)itemTriple.Object).Uri;
                DateTime published = DateTime.Parse(((ILiteralNode)publishedTriple.Object).Value);
                int count = int.Parse(((ILiteralNode)countTriple.Object).Value);

                _items.Add(new Tuple<Uri, DateTime, int?>(itemUri, published, count));

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
