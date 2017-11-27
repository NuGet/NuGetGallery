// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Shingle;

namespace NuGet.Indexing
{
    public class ShingledIdentifierAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerInvariantFilter(new ShingleFilter(new DotTokenizer(reader)));
        }
    }
}
