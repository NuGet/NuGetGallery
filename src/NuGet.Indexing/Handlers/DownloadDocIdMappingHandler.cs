// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    /// <summary>
    /// Maps the download count from docId, assuming the downloads is already populated with
    /// downloads per package Id.
    /// </summary>
    public class DownloadDocIdMappingHandler : IIndexReaderProcessorHandler
    {
        private readonly Downloads _downloads;

        public DownloadDocIdMappingHandler(Downloads downloads)
        {
            _downloads = downloads;
        }

        public bool SkipDeletes => true;

        public void Begin(IndexReader indexReader)
        {
        }

        public void End(IndexReader indexReader)
        {
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _downloads[perIndexDocumentNumber] = _downloads[id];
            }
            else
            {
                _downloads[perIndexDocumentNumber] = null;
            }
        }
    }
}
