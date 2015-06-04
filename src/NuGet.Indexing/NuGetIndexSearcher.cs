// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class NuGetIndexSearcher : IndexSearcher
    {
        public NuGetIndexSearcher(IndexReader reader, Tuple<IDictionary<string, Filter>, IDictionary<string, Filter>> filters, IDictionary<string, Tuple<OpenBitSet, OpenBitSet>> latestBitSets, JArray[] versionListsByDoc, JArray[] versionsByDoc) 
            : base(reader)
        {
            Filters = filters;
            LatestBitSets = latestBitSets;
            VersionListsByDoc = versionListsByDoc;
            VersionsByDoc = versionsByDoc;
        }

        public Tuple<IDictionary<string, Filter>, IDictionary<string, Filter>> Filters { get; private set; }
        public IDictionary<string, Tuple<OpenBitSet, OpenBitSet>> LatestBitSets { get; private set; }
        public JArray[] VersionListsByDoc { get; private set; }
        public JArray[] VersionsByDoc { get; private set; }
    }
}