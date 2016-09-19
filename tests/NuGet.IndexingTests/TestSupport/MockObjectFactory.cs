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

        public static Document GetBasicDocument(int MockId)
        {
            var mockDocument = new Document();
            mockDocument.Add(new Field(Constants.LucenePropertyId, MockPrefix + Constants.LucenePropertyId + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyVersion, MockPrefix + Constants.LucenePropertyVersion + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyDescription, MockPrefix + Constants.LucenePropertyDescription + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertySummary, MockPrefix + Constants.LucenePropertySummary + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyTitle, MockPrefix + Constants.LucenePropertyTitle + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyIconUrl, MockPrefix + Constants.LucenePropertyIconUrl + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyLicenseUrl, MockPrefix + Constants.LucenePropertyLicenseUrl + MockId, Field.Store.YES, Field.Index.NO));
            mockDocument.Add(new Field(Constants.LucenePropertyProjectUrl, MockPrefix + Constants.LucenePropertyProjectUrl + MockId, Field.Store.YES, Field.Index.NO));

            return mockDocument;
        }

        public static Mock<IndexReader> CreateMockIndexReader(int numberOfDocs)
        {
            var mockIndexReader = new Mock<IndexReader>();
            var numDocs = numberOfDocs;

            mockIndexReader.Setup(x => x.MaxDoc).Returns(numDocs);
            mockIndexReader.Setup(x => x.TermDocs()).Returns((TermDocs)null);
            mockIndexReader.Setup(x => x.NumDocs()).Returns(numDocs);

            return mockIndexReader;
        }

        public static Mock<ILogger> CreateMockLogger()
        {
            return new Mock<ILogger>();
        }
    }
}
