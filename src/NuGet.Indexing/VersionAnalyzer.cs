// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Lucene.Net.Analysis;

namespace NuGet.Indexing
{
    public class VersionAnalyzer : Analyzer
    {
        private readonly bool _caseSensitive;

        public VersionAnalyzer(bool caseSensitive)
        {
            _caseSensitive = caseSensitive;
        }

        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            TokenStream stream =  new SemanticVersionFilter(new KeywordTokenizer(reader));

            if (!_caseSensitive)
            {
                stream = new LowerInvariantFilter(stream);
            }

            return stream;
        }
    }
}
