using Lucene.Net.Analysis;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    public class TagsAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new DotTokenizer(reader));
        }
    }
}
