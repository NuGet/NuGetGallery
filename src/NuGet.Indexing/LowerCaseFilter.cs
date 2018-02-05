// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Lucene.Net.Analysis;

namespace NuGet.Indexing
{
    [Obsolete("Lucene.Net.Analysis.LowerCaseFilter uses char.ToLower, which is incorrect.")]
    public class LowerCaseFilter : TokenFilter
    {
        public LowerCaseFilter(TokenStream input) : base(input)
        {
        }

        public override bool IncrementToken()
        {
            throw new NotImplementedException();
        }
    }
}
