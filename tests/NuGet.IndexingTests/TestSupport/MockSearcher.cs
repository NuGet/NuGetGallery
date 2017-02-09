// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Indexing;
using NuGet.Services.Configuration;

namespace NuGet.IndexingTests.TestSupport
{

    public class MockSearcher : NuGetIndexSearcher
    {
        public MockSearcher(string indexName, int numDocs, Dictionary<string, string> commitUserData, VersionResult[] versions = null)
            : base(manager: InitNuGetSearcherManager(indexName),
                  reader: MockObjectFactory.CreateMockIndexReader(numDocs).Object,
                  commitUserData: commitUserData,
                  curatedFeeds: new Dictionary<string, Filter>(),
                  latest: null, docIdMapping: null,
                  downloads: null,
                  versions: versions,
                  rankings: null,
                  context: null,
                  latestBitSet: Constants.LatestBitSet,
                  latestStableBitSet: Constants.LatestStableBitSet,
                  owners: Constants.EmptyOwnersResult)
        {
            MockObjectFactory.MockPrefix = Constants.MockBase;
        }

        private static NuGetSearcherManager InitNuGetSearcherManager(string indexName)
        {
            var mockSearcherManager = new Mock<NuGetSearcherManager>(new Mock<ILogger>().Object, null, null,
                int.MaxValue, int.MaxValue)
            {
                CallBase = true
            };

            mockSearcherManager.Setup(x => x.IndexName).Returns(indexName);
            mockSearcherManager.Object.RegistrationBaseAddress[Constants.SchemeName] = new Uri(Constants.BaseUri);

            return mockSearcherManager.Object;
        }

        public override Document Doc(int id)
        {
            return MockObjectFactory.GetBasicDocument(id);
        }

        public override Explanation Explain(Query query, int doc)
        {
            var explanation = new Explanation((float)1.0, Constants.MockExplanationBase);
            return explanation;
        }
    }
}
