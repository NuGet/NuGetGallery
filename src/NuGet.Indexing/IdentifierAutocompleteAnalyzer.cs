// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.NGram;

namespace NuGet.Indexing
{
    public class IdentifierAutocompleteAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new EdgeNGramTokenFilter(
                new LowerInvariantFilter(
                    new CamelCaseFilter(new DotTokenizer(reader))),
                side: Side.FRONT,
                minGram: 1,
                maxGram: 8);
        }
    }
}