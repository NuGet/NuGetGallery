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
            return new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "Title", new TitleAnalyzer() }
            };
        }

        class TitleAnalyzer : Analyzer
        {
            private readonly Analyzer innerAnalyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);

            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                // Split the title based on IdSeparators, then run it through the standardAnalyzer
                string title = reader.ReadToEnd();
                string partiallyTokenized = String.Join(" ", title.Split(LuceneIndexingService.IdSeparators, StringSplitOptions.RemoveEmptyEntries));
                return innerAnalyzer.TokenStream(fieldName, new StringReader(partiallyTokenized));
            }
        }
    }
}