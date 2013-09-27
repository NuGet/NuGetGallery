using Lucene.Net.Analysis;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    public class DescriptionAnalyzer : Analyzer
    {
        private static ISet<string> stopWords = new HashSet<string> 
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", 
            "if", "in", "into", "is", "it", "no", "not", "of", "on", "or", "such",
            "that", "the", "their", "then", "there", "these", "they", "this", "to", 
            "was", "will", "with"
        };

        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new StopFilter(true,
                new LowerCaseFilter(
                    new WhitespaceTokenizer(reader)),
                    stopWords);
        }
    }
}
