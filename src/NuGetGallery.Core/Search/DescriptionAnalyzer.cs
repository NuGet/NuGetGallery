using Lucene.Net.Analysis;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    public class DescriptionAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new StopFilter(true, new LowerCaseFilter(new CamelCaseFilter(new DotTokenizer(reader))), TokenizingHelper.GetStopWords());
        }
    }
}
