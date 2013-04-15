using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;

namespace NuGetGallery
{
    public class PerFieldAnalyzer : PerFieldAnalyzerWrapper
    {
        public PerFieldAnalyzer()
            : base(new StandardAnalyzer(LuceneCommon.LuceneVersion), CreateFieldAnalyzers())
        {
        }

        private static IDictionary CreateFieldAnalyzers()
        {
            // For idAnalyzer we use the 'standard analyzer' but with no stop words (In, Of, The, etc are indexed).
            var stopWords = new Hashtable();
            StandardAnalyzer idAnalyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion, stopWords);

            return new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "Id", idAnalyzer },
                { "Title", new TitleAnalyzer() },
            };
        }

        class TitleAnalyzer : Analyzer
        {
            private readonly StandardAnalyzer innerAnalyzer;

            public TitleAnalyzer()
            {
                // For innerAnalyzer we use the 'standard analyzer' but with no stop words (In, Of, The, etc are indexed).
                var stopWords = new Hashtable();
                innerAnalyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion, stopWords);
            }

            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                // Split the title based on IdSeparators, then run it through the innerAnalyzer
                string title = reader.ReadToEnd();
                string partiallyTokenized = String.Join(" ", title.Split(PackageIndexEntity.IdSeparators, StringSplitOptions.RemoveEmptyEntries));
                return innerAnalyzer.TokenStream(fieldName, new StringReader(partiallyTokenized));
            }
        }
    }
}