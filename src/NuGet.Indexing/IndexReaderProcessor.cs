// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class IndexReaderProcessor
    {
        private readonly List<IIndexReaderProcessorHandler> _handlers =
            new List<IIndexReaderProcessorHandler>();

        private readonly bool _enumerateSubReaders;

        public IndexReaderProcessor(bool enumerateSubReaders)
        {
            _enumerateSubReaders = enumerateSubReaders;
        }

        public void AddHandler(IIndexReaderProcessorHandler handler)
        {
            _handlers.Add(handler);
        }

        public void Process(IndexReader indexReader)
        {
            var perIndexDocumentNumber = 0;

            foreach (var handler in _handlers)
            {
                handler.Begin(indexReader);
            }

            if (_enumerateSubReaders && indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    ProcessReader(segmentReader, segmentReader.SegmentName, ref perIndexDocumentNumber);
                }
            }
            else
            {
                ProcessReader(indexReader, string.Empty, ref perIndexDocumentNumber);
            }

            foreach (var handler in _handlers)
            {
                handler.End(indexReader);
            }
        }

        void ProcessReader(IndexReader indexReader, string readerName, ref int perIndexDocumentNumber)
        {
            for (int perReaderDocumentNumber = 0; perReaderDocumentNumber < indexReader.MaxDoc; perReaderDocumentNumber++)
            {
                if (indexReader.IsDeleted(perReaderDocumentNumber))
                {
                    ProcessDocument(indexReader, readerName, perReaderDocumentNumber, perIndexDocumentNumber, null, isDelete: true);
                }
                else
                {
                    Document document = indexReader.Document(perReaderDocumentNumber);
                    ProcessDocument(indexReader, readerName, perReaderDocumentNumber, perIndexDocumentNumber, document, isDelete: false);
                }

                perIndexDocumentNumber++;
            }
        }

        void ProcessDocument(IndexReader indexReader,
            string readerName,
            int perReaderDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            bool isDelete)
        {
            NuGetVersion version = document != null ? GetVersion(document) : null;
            string id = document != null ? GetId(document) : null;

            foreach (var handler in _handlers)
            {
                if (isDelete && handler.SkipDeletes)
                {
                    continue;
                }

                handler.Process(indexReader, readerName, perReaderDocumentNumber, perIndexDocumentNumber, document, id, version);
            }
        }

        private static NuGetVersion GetVersion(Document document)
        {
            string version = document.Get(MetadataConstants.LuceneMetadata.VerbatimVersionPropertyName);
            return (version == null) ? null : new NuGetVersion(version);
        }

        private static string GetId(Document document)
        {
            string id = document.Get("Id");
            string ns = document.Get("Namespace");
            string fullname = (ns == null) ? id : string.Format("{0}/{1}", ns, id);
            return (fullname == null) ? null : fullname.ToLowerInvariant();
        }
    }
}
