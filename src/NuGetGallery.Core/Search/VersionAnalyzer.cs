using Lucene.Net.Analysis;
using System.IO;

namespace NuGetGallery
{
    public class VersionAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new SemanticVersionFilter(new KeywordTokenizer(reader));
        }
    }
}
