using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using System.IO;

namespace NuGetGallery
{
    public class SemanticVersionFilter : TokenFilter
    {
        ITermAttribute _termAttribute;

        public SemanticVersionFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            string version = _termAttribute.Term;
            string normalizedVersion = SemanticVersionExtensions.Normalize(version);
            _termAttribute.SetTermBuffer(normalizedVersion);

            return true;
        }
    }
}
