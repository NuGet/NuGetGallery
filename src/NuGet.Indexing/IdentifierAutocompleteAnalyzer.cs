// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
﻿using Lucene.Net.Analysis;
using Lucene.Net.Analysis.NGram;
using System.IO;

namespace NuGet.Indexing
{
    public class IdentifierAutocompleteAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new EdgeNGramTokenFilter(new LowerCaseFilter(new CamelCaseFilter(new DotTokenizer(reader))), Side.FRONT, 1, 8);
        }
    }
}