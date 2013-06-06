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

        private static IDictionary<string, Analyzer> CreateFieldAnalyzers()
        {
            return new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "Id", new StandardAnalyzer(LuceneCommon.LuceneVersion, new HashSet<string>()) },
                { "Title", new TitleAnalyzer() },
                { "Description", new DescriptionAnalyzer() },
                { "Tags", new DescriptionAnalyzer() },
            };
        }

        //  similar to a StandardAnalyzer except this allows special characters (like C++)
        //  note the base tokenization is now just whitespace in this case

        class TitleAnalyzer : Analyzer
        {
            private static readonly WhitespaceAnalyzer whitespaceAnalyzer = new WhitespaceAnalyzer();

            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                // Split the title based on IdSeparators, then run it through the innerAnalyzer
                string title = reader.ReadToEnd();
                string partiallyTokenized = String.Join(" ", title.Split(PackageIndexEntity.IdSeparators, StringSplitOptions.RemoveEmptyEntries));
                TokenStream result = whitespaceAnalyzer.TokenStream(fieldName, new StringReader(partiallyTokenized));
                result = new LowerCaseFilter(result);
                return result;
            }
        }

        //  similar to our TitleAnalyzer except we want to ignore stop words in the description

        class DescriptionAnalyzer : Analyzer
        {
            private static ISet<string> stopWords = new HashSet<string> 
            {
                "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", 
                "if", "in", "into", "is", "it", "no", "not", "of", "on", "or", "such",
                "that", "the", "their", "then", "there", "these", "they", "this", "to", 
                "was", "will", "with"
            };

            private static readonly WhitespaceAnalyzer whitespaceAnalyzer = new WhitespaceAnalyzer();

            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                TokenStream result = whitespaceAnalyzer.TokenStream(fieldName, reader);
                result = new LowerCaseFilter(result);
                result = new StopFilter(true, result, stopWords);
                return result;
            }
        }
    }
}
