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
        bool _skipDeletes;
        List<IIndexReaderProcessorHandler> _handlers;
        bool _enumerateSubReaders;

        public IndexReaderProcessor(bool enumerateSubReaders, bool skipDeletes)
        {
            _skipDeletes = skipDeletes;
            _handlers = new List<IIndexReaderProcessorHandler>();
            _enumerateSubReaders = enumerateSubReaders;
        }

        public void AddHandler(IIndexReaderProcessorHandler handler)
        {
            _handlers.Add(handler);
        }

        public void Process(IndexReader indexReader)
        {
            foreach (var handler in _handlers)
            {
                handler.Begin(indexReader);
            }

            if (_enumerateSubReaders && indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    ProcessReader(segmentReader, segmentReader.SegmentName);
                }
            }
            else
            {
                ProcessReader(indexReader, string.Empty);
            }

            foreach (var handler in _handlers)
            {
                handler.End(indexReader);
            }
        }

        void ProcessReader(IndexReader indexReader, string readerName)
        {
            for (int n = 0; n < indexReader.MaxDoc; n++)
            {
                if (indexReader.IsDeleted(n))
                {
                    if (_skipDeletes)
                    {
                        continue;
                    }

                    ProcessDocument(indexReader, readerName, n, null);
                }
                else
                {
                    Document document = indexReader.Document(n);
                    ProcessDocument(indexReader, readerName, n, document);
                }
            }
        }

        void ProcessDocument(IndexReader indexReader, string readerName, int n, Document document)
        {
            NuGetVersion version = document != null ? GetVersion(document) : null;
            string id = document != null ? GetId(document) : null;

            foreach (var handler in _handlers)
            {
                handler.Process(indexReader, readerName, n, document, id, version);
            }
        }

        static NuGetVersion GetVersion(Document document)
        {
            string version = document.Get("Version");
            return (version == null) ? null : new NuGetVersion(version);
        }

        static string GetId(Document document)
        {
            string id = document.Get("Id");
            string ns = document.Get("Namespace");
            string fullname = (ns == null) ? id : string.Format("{0}/{1}", ns, id);
            return (fullname == null) ? null : fullname.ToLowerInvariant();
        }
    }
}
