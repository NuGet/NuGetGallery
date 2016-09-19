// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Indexing;

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
            var searcherManager = new NuGetSearcherManager(indexName, new Mock<ILogger>().Object, directory: null, loader: null, azureDirectorySynchronizer: null);

            searcherManager.RegistrationBaseAddress[Constants.SchemeName] = new Uri(Constants.BaseUri);

            return searcherManager;
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
