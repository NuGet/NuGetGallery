using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class PackageAnalyzer : PerFieldAnalyzerWrapper
    {
        public PackageAnalyzer()
            : base(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), CreateFieldAnalyzers())
        {
        }

        private static IDictionary<string, Analyzer> CreateFieldAnalyzers()
        {
            return new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "@type", new KeywordAnalyzer() },
                { "Id", new IdentifierKeywordAnalyzer() },
                { "IdAutocomplete", new IdentifierAutocompleteAnalyzer() },
                { "IdAutocompletePhrase", new IdentifierAutocompleteAnalyzer() },
                { "Facet", new IdentifierKeywordAnalyzer() },
                { "TokenizedId", new IdentifierAnalyzer() },
                { "ShingledId", new ShingledIdentifierAnalyzer() },
                { "Version", new VersionAnalyzer() },
                { "Title", new DescriptionAnalyzer() },
                { "Description", new DescriptionAnalyzer() },
                { "Summary", new DescriptionAnalyzer() },
                { "Authors", new DescriptionAnalyzer() },
                { "Owners", new DescriptionAnalyzer() },
                { "Tags", new TagsAnalyzer() },
                { "__default", new KeywordAnalyzer() } // The "__default" field is only used during initial query parsing. It should just be tokenized as a keyword for later processing.
            };
        }
    }
}
