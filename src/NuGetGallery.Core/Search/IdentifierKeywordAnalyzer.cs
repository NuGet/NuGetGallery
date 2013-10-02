using Lucene.Net.Analysis;
using System.IO;

namespace NuGetGallery
{
    public class IdentifierKeywordAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new KeywordTokenizer(reader));
        }
    }
}
