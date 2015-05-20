// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Indexing
{
    public class DescriptionAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new StopFilter(true, new LowerCaseFilter(new CamelCaseFilter(new DotTokenizer(reader))), TokenizingHelper.GetStopWords());
        }
    }
}
