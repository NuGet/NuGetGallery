// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using Microsoft.Extensions.Logging;
using Moq;

namespace NuGet.IndexingTests.TestSupport
{
    public static class MockObjectFactory
    {
        public static string MockPrefix = "Mock";

        public static Document GetSemVerDocument(int MockId, bool listed = true, string semVerLevel = null)
        {
            var mockDocument = GetBasicDocument(MockId, listed);
            mockDocument.Add(new Field(Constants.LucenePropertySemVerLevel, semVerLevel == null ? Constants.SemVerLevel2Value : semVerLevel, Field.Store.YES, Field.Index.NO));

            return mockDocument;
        }

        public static Document GetBasicDocument(int MockId, bool listed = true)
        {
            var mockDocument = new Document();
            mockDocument.Add(new Field(Constants.LucenePropertyId, MockPrefix + Constants.LucenePropertyId + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyFullVersion, MockPrefix + Constants.LucenePropertyFullVersion + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyNormalizedVersion, MockPrefix + Constants.LucenePropertyNormalizedVersion + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyOriginalVersion, MockPrefix + Constants.LucenePropertyOriginalVersion + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyDescription, MockPrefix + Constants.LucenePropertyDescription + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertySummary, MockPrefix + Constants.LucenePropertySummary + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyTitle, MockPrefix + Constants.LucenePropertyTitle + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyIconUrl, MockPrefix + Constants.LucenePropertyIconUrl + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyLicenseUrl, MockPrefix + Constants.LucenePropertyLicenseUrl + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyProjectUrl, MockPrefix + Constants.LucenePropertyProjectUrl + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyListed, listed ? "true" : "false", Field.Store.YES, Field.Index.NO));

            return mockDocument;
        }

        public static Mock<IndexReader> CreateMockIndexReader(int numberOfDocs, int numberOfSubReaders = 0)
        {
            var mockIndexReader = new Mock<IndexReader>();
            var numDocs = numberOfDocs;

            mockIndexReader.Setup(x => x.MaxDoc).Returns(numDocs);
            mockIndexReader.Setup(x => x.TermDocs()).Returns((TermDocs)null);
            mockIndexReader.Setup(x => x.NumDocs()).Returns(numDocs);
            mockIndexReader.Setup(x => x.GetSequentialSubReaders()).Returns(MakeFakeSubReaders(numberOfSubReaders));

            return mockIndexReader;
        }

        private static SegmentReader[] MakeFakeSubReaders(int numberOfReaders)
        {
            if (numberOfReaders <= 0)
            {
                return null;
            }

            var readers = new SegmentReader[numberOfReaders];

            for (var i = 0; i < numberOfReaders; i ++)
            {
                var mockReader = new Mock<SegmentReader>();
                mockReader.Setup(x => x.SegmentName).Returns(Constants.SegmentReaderPrefix + i);
                readers[i] = mockReader.Object;
            }

            return readers;
        }

        public static Mock<ILogger> CreateMockLogger()
        {
            return new Mock<ILogger>();
        }
    }
}
