// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet.Indexing;
using Lucene.Net.Documents;
using NuGet.Versioning;

namespace NuGet.IndexingTests.TestSupport
{
    public class Constants
    {
        public static readonly string BaseUri = "http://testuri/";
        public static readonly string LucenePropertyIconUrl = "IconUrl";
        public static readonly string LucenePropertyId = "Id";
        public static readonly string LucenePropertyDescription = "Description";
        public static readonly string LucenePropertyLicenseUrl = "LicenseUrl";
        public static readonly string LucenePropertyListed = "Listed";
        public static readonly string LucenePropertyProjectUrl = "ProjectUrl";
        public static readonly string LucenePropertySummary = "Summary";
        public static readonly string LucenePropertyTitle = "Title";
        public static readonly string LucenePropertyVersion = "Version";
        public static readonly string LucenePropertySemVerLevel = "SemVerLevel";
        public static readonly string MockBase = "Mock";
        public static readonly string MockExplanationBase = MockBase + "Explanation";
        public static readonly string Query = "test";
        public static readonly string RankingsIdPrefix = "testId";
        public static readonly string RankingsSegmentName = "testReader";
        public static readonly string SchemeName = "test";
        public static readonly string SegmentReaderPrefix = "SegmentReader";
        public static readonly string SemVerLevel2Value = "2.0.0";

        public static readonly ScoreDoc[] ScoreDocs = {
                new ScoreDoc(0, (float)1.0),
                new ScoreDoc(1, (float)0.5)
            };

        public static readonly VersionResult[] VersionResults = {
                new VersionResult(),
                new VersionResult()
            };


        // These two lists should be used together to create the full matrix of documents
        public static readonly Document[] FullDocMatrix =
        {
            MockObjectFactory.GetBasicDocument(0, listed:true),
            MockObjectFactory.GetBasicDocument(1, listed:false),
            MockObjectFactory.GetSemVerDocument(2, listed: true),
            MockObjectFactory.GetSemVerDocument(3, listed: false),
            MockObjectFactory.GetBasicDocument(4, listed:true),
            MockObjectFactory.GetBasicDocument(5, listed:false),
            MockObjectFactory.GetSemVerDocument(6, listed: true),
            MockObjectFactory.GetSemVerDocument(7, listed: false)
        };

        public static readonly NuGetVersion[] FullVersionMatrix =
        {
            new NuGetVersion("1.0.0"),
            new NuGetVersion("1.0.0"),
            new NuGetVersion("1.0.0"),
            new NuGetVersion("1.0.0"),
            new NuGetVersion("1.0.0-something"),
            new NuGetVersion("1.0.0-something"),
            new NuGetVersion("1.0.0-something"),
            new NuGetVersion("1.0.0-something")
        };

        public static readonly OpenBitSet LatestBitSet = new OpenBitSet(10);

        public static readonly OpenBitSet LatestStableBitSet = new OpenBitSet(10);

        public static readonly OpenBitSet LatestSemVer2BitSet = new OpenBitSet(10);

        public static readonly OpenBitSet LatestStableSemVer2BitSet = new OpenBitSet(10);

        public static readonly OwnersResult EmptyOwnersResult = new OwnersResult(new HashSet<string>(), new Dictionary<string, HashSet<string>>(), new Dictionary<string, IDictionary<string, DynamicDocIdSet>>());
    }
}
