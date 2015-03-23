using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace NuGet.Indexing
{
    /// <summary>
    /// Represents a document and a parsed representation of the Facets contained within.
    /// </summary>
    public class FacetedDocument
    {
        private readonly ISet<string> _facets;
        private SemanticVersion _version;
        
        public IndexDocumentData Data { get; private set; }
        public IEnumerable<string> DocFacets { get { return _facets; } }
        public bool Dirty { get; private set; }
        public bool IsNew { get; private set; }
        public SemanticVersion Version
        {
            get
            {
                if (_version == null)
                {
                    _version = SemanticVersion.Parse(Data.Package.NormalizedVersion);
                }
                return _version;
            }
        }
        public string Id
        {
            get
            {
                return Data.Package.PackageRegistration.Id;
            }
        }

        public FacetedDocument(Document doc)
        {
            Data = IndexDocumentData.FromDocument(doc);
            Dirty = IsNew = false;
            _facets = ParseFacets(doc.GetFields(Facets.FieldName));
        }

        public FacetedDocument(IndexDocumentData data)
        {
            Data = data;
            Dirty = IsNew = true;

            _facets = new HashSet<string>();
        }

        public FacetedDocument(IndexDocumentData data, IEnumerable<Field> existingFacets)
        {
            Data = data;
            Dirty = IsNew = true;

            _facets = ParseFacets(existingFacets);
        }

        public bool HasFacet(string facet)
        {
            return _facets.Contains(facet);
        }

        public void RemoveFacet(string facet)
        {
            if (_facets.Contains(facet))
            {
                _facets.Remove(facet);
                Dirty = true;
            }
        }

        public void AddFacets(IEnumerable<string> facets)
        {
            foreach (string facet in facets)
            {
                AddFacet(facet);
            }
        }

        public void AddFacet(string facet)
        {
            if (!_facets.Contains(facet))
            {
                _facets.Add(facet);
                Dirty = true;
            }
        }

        private ISet<string> ParseFacets(IEnumerable<Field> fields)
        {
            if (fields != null)
            {
                return new HashSet<string>(
                    fields.Select(f => f.StringValue),
                    StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                return new HashSet<string>();
            }
        }

        // Gets a query that returns exactly this document
        public Query GetQuery()
        {
            return NumericRangeQuery.NewIntRange(
                "Key", Data.Package.Key, Data.Package.Key, minInclusive: true, maxInclusive: true);
        }
    }
}
