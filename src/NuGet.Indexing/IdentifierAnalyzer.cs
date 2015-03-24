using Lucene.Net.Analysis;
using System.IO;

namespace NuGet.Indexing
{
    public class IdentifierAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new CamelCaseFilter(new DotTokenizer(reader)));
        }
    }
}
