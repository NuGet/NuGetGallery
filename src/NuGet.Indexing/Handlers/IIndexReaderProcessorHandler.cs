// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public interface IIndexReaderProcessorHandler
    {
        bool SkipDeletes { get; }

        void Begin(IndexReader indexReader);
        void End(IndexReader indexReader);

        /// <summary>
        /// Process auxilliary data during index low.
        /// </summary>
        /// <param name="indexReader">The index reader.</param>
        /// <param name="readerName">The sub reader name, or string.Empty if there are no sub readers.</param>
        /// <param name="perSegmentDocumentNumber">The index in the sub reader.</param>
        /// <param name="perIndexDocumentNumber">The index in the global index.</param>
        /// <param name="document">The Lucene document.</param>
        /// <param name="packageId">The NuGet package Id.</param>
        /// <param name="version">The NuGet package version.</param>
        void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string packageId,
            NuGetVersion version);
    }
}
