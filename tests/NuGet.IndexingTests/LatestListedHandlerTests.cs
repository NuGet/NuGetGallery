// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using NuGet.Versioning;
using Xunit;

namespace NuGet.IndexingTests
{
    public class LatestListedHandlerTests
    {
        [Theory]
        [MemberData(nameof(DocumentsAreFilteredData))]
        public void DocumentsAreFilteredTest(
            bool includeUnlisted,
            bool includePrerelease,
            bool includeSemVer2,
            string readerName,
            string id,
            Document[] documents,
            NuGetVersion[] versions,
            int numSubReadersToFake,
            int expectedNumDocsInResult)
        {
            // Arrange
            var handler = new LatestListedHandler(includeUnlisted, includePrerelease, includeSemVer2);
            var fakeReader = MockObjectFactory.CreateMockIndexReader(5, numberOfSubReaders: numSubReadersToFake).Object;

            // Act
            handler.Begin(fakeReader);
            // Pick a random subreader for each document
            var docNumber = 0;
            var rnd = new Random(10000);
            for (var i = 0; i < documents.Length; i++)
            {
                handler.Process(indexReader: fakeReader,
                    readerName: Constants.SegmentReaderPrefix + rnd.Next(numSubReadersToFake),
                    perSegmentDocumentNumber: docNumber,
                    perIndexDocumentNumber: docNumber,
                    document: documents[i],
                    id: id + docNumber,
                    version: versions[i]);
                docNumber++;
            }

            handler.End(fakeReader);
            var resultFilter = handler.Result;

            // Assert
            var docsInResult = new HashSet<int>();
            var listReaders = fakeReader.GetSequentialSubReaders();
            foreach (var reader in listReaders)
            {
                var bitSet = resultFilter.GetDocIdSet(reader);
                var bitSetIterator = bitSet.Iterator();
                if (bitSetIterator != DocIdSet.EMPTY_DOCIDSET.Iterator() && bitSetIterator != null)
                {
                    if (bitSetIterator.DocID() == -1)
                    {
                        // we aren't on a doc yet, so call NextDoc to advance to the first doc
                        bitSetIterator.NextDoc();
                    }

                    while (bitSetIterator.DocID() != DocIdSetIterator.NO_MORE_DOCS)
                    {
                        docsInResult.Add(bitSetIterator.DocID());
                        bitSetIterator.NextDoc();
                    }
                }
            }

            Assert.Equal(expectedNumDocsInResult, docsInResult.Count);
        }


        [Theory]
        [MemberData(nameof(SemVerLevel2IsCorrectData))]
        public void SemVerLevel2IsCorrectTest(Document doc, bool expectedToBeSemVer2)
        {
            Assert.Equal(LatestListedHandler.IsSemVer2(doc), expectedToBeSemVer2);
        }

        [Theory]
        [MemberData(nameof(ListedIsCorrectData))]
        public void ListedIsCorrectTest(Document doc, bool expectedToBeListed)
        {
            Assert.Equal(LatestListedHandler.GetListed(doc), expectedToBeListed);
        }


        public static IEnumerable<object[]> DocumentsAreFilteredData
        {
            get
            {
                // Include unlisted, prerelease, semver2
                yield return new object[]
                {
                    true,           // includeUnlisted
                    true,           // includePrerelease
                    true,           // includeSemVer2
                    "includeAll",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    8
                };

                // Include unlisted, prerelease
                yield return new object[]
                {
                    true,           // includeUnlisted
                    true,           // includePrerelease
                    false,          // includeSemVer2
                    "includeUnlistedPrerelease",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    4
                };

                // Include unlisted, semver2
                yield return new object[]
                {
                    true,           // includeUnlisted
                    false,          // includePrerelease
                    true,           // includeSemVer2
                    "includeUnlistedSemVer2",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    4
                };
                // Include unlisted
                yield return new object[]
                {
                    true,           // includeUnlisted
                    false,          // includePrerelease
                    false,          // includeSemVer2
                    "includeUnlisted",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    2
                };

                // Include Prerelease, SemVer2
                yield return new object[]
                {
                    false,          // includeUnlisted
                    true,           // includePrerelease
                    true,           // includeSemVer2
                    "includePrereleaseSemVer2",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    4
                };

                // Include prerelease
                yield return new object[]
                {
                    false,          // includeUnlisted
                    true,           // includePrerelease
                    false,          // includeSemVer2
                    "includePrerelease",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    2
                };

                // Include semver2
                yield return new object[]
                {
                    false,          // includeUnlisted
                    false,          // includePrerelease
                    true,           // includeSemVer2
                    "includeSemVer2",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    2
                };

                // Include unlisted
                yield return new object[]
                {
                    false,          // includeUnlisted
                    false,          // includePrerelease
                    false,          // includeSemVer2
                    "includeNone",
                    "fake.Package",
                    Constants.FullDocMatrix,
                    Constants.FullVersionMatrix,
                    4,
                    1
                };
            }
        }

        public static IEnumerable<object[]> ListedIsCorrectData
        {
            get
            {
                // listed is listed
                yield return new object[]
                {
                    MockObjectFactory.GetBasicDocument(0, listed: true),
                    true
                };

                yield return new object[]
                {
                    MockObjectFactory.GetBasicDocument(1, listed: false),
                    false
                };
            }
        }

        public static IEnumerable<object[]> SemVerLevel2IsCorrectData
        {
            get
            {
                // old Document is not semver2
                yield return new object[]
                {
                    MockObjectFactory.GetBasicDocument(0),
                    false
                };

                // SemVer2 document is SemVer2
                yield return new object[]
                {
                    MockObjectFactory.GetSemVerDocument(1),
                    true
                };

                // new NOT SemVer2 document is not SemVer2
                yield return new object[]
                {
                    MockObjectFactory.GetSemVerDocument(1, true, "1.0.0"),
                    false
                };

                // Value > 2.0.0 is not SemVer2
                yield return new object[]
                {
                    MockObjectFactory.GetSemVerDocument(1, true, "3.0.0"),
                    false
                };

                // nonsense value is not SemVer2
                yield return new object[]
                {
                    MockObjectFactory.GetSemVerDocument(1, true, "catdog"),
                    false
                };
            }
        }
    }
}
