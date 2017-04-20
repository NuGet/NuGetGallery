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
        public MockSearcher(string indexName, int numDocs, Dictionary<string, string> commitUserData, VersionResult[] versions = null, DateTime? reloadTime = null, Dictionary<string, DateTime?> lastModifiedTimeForAuxFiles = null, string machineName = "TestMachine")
            : base(manager: InitNuGetSearcherManager(indexName, reloadTime, lastModifiedTimeForAuxFiles, machineName),
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
                  latestSemVer2BitSet: Constants.LatestSemVer2BitSet,
                  latestStableSemVer2BitSet: Constants.LatestStableSemVer2BitSet,
                  owners: Constants.EmptyOwnersResult)
        {
            MockObjectFactory.MockPrefix = Constants.MockBase;
        }

        private static NuGetSearcherManager InitNuGetSearcherManager(string indexName, DateTime? reloadTime, Dictionary<string, DateTime?> lastModifiedTimeForAuxFiles, string machineName)
        {
            var mockSearcherManager = new Mock<NuGetSearcherManager>(new Mock<ILogger>().Object, null, null,
                int.MaxValue, int.MaxValue)
            {
                CallBase = true
            };

            var time = reloadTime.HasValue ? reloadTime.Value : DateTime.UtcNow;
            var mockAuxiliaryFiles = new Mock<AuxiliaryFiles>(null);
            mockAuxiliaryFiles.Setup(y => y.LastModifiedTimeForFiles).Returns(lastModifiedTimeForAuxFiles);

            mockSearcherManager.Setup(x => x.IndexName).Returns(indexName);
            mockSearcherManager.Object.RegistrationBaseAddresses.LegacyHttp = new Uri(Constants.BaseUriHttp);
            mockSearcherManager.Object.RegistrationBaseAddresses.LegacyHttps = new Uri(Constants.BaseUriHttps);
            mockSearcherManager.Object.RegistrationBaseAddresses.SemVer2Http = new Uri(Constants.BaseUriSemVer2Http);
            mockSearcherManager.Object.RegistrationBaseAddresses.SemVer2Https = new Uri(Constants.BaseUriSemVer2Https);
            mockSearcherManager.Setup(x => x.LastIndexReloadTime).Returns(time);
            mockSearcherManager.Setup(x => x.LastAuxiliaryDataLoadTime).Returns(time);
            mockSearcherManager.Setup(x => x.MachineName).Returns(machineName);
            mockSearcherManager.Setup(x => x.AuxiliaryFiles).Returns(mockAuxiliaryFiles.Object);

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
