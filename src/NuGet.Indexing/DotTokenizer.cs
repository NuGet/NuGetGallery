using Lucene.Net.Analysis;
using System;
using System.IO;

namespace NuGet.Indexing
{
    public class DotTokenizer : CharTokenizer
    {
        public DotTokenizer(TextReader input)
            : base(input)
        {
        }

        protected override bool IsTokenChar(char c)
        {
            return !(Char.IsWhiteSpace(c) || c == '.' || c == '-' || c == ',' || c == ';' || c == ':' || c == '\'');
        }
    }
}
