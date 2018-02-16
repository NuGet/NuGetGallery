// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Analysis;
using System.IO;

namespace NuGet.Indexing
{
    public class TagsAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new DotTokenizer(reader));
        }
    }
}
