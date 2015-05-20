// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace NuGet.Indexing
{
    public class PublicFilter : QueryWrapperFilter
    {
        public PublicFilter() : base(new TermQuery(new Term("Visibility", "Public")))
        {
        }
    }
}
