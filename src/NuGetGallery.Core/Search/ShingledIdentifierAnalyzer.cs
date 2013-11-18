using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Shingle;
using System.IO;

namespace NuGetGallery
{
    public class ShingledIdentifierAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new ShingleFilter(new DotTokenizer(reader)));
        }
    }
}
