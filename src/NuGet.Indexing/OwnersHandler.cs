// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class OwnersHandler : IIndexReaderProcessorHandler
    {
        Lucene.Net.Store.Directory _directory;
        IndexWriter _writer;
        IDictionary<string, HashSet<string>> _owners;
        List<int> _deletes;

        public OwnersHandler(IDictionary<string, HashSet<string>> owners)
        {
            _owners = owners;
            _deletes = new List<int>();
        }

        public void Begin(IndexReader indexReader)
        {
            _directory = new RAMDirectory();
            _writer = new IndexWriter(_directory, new PackageAnalyzer(), IndexWriter.MaxFieldLength.UNLIMITED);
        }

        public void End(IndexReader indexReader)
        {
            _writer.Commit();
            _writer.Dispose();
        }

        public void Process(IndexReader indexReader, string readerName, int n, Document document, string id, NuGetVersion version)
        {
            Document newDocument = new Document();

            HashSet<string> registrationOwners;
            if (id != null && _owners.TryGetValue(id, out registrationOwners))
            {
                foreach (string registrationOwner in registrationOwners)
                {
                    newDocument.Add(new Field("Owner", registrationOwner, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
                }
            }
            else
            {
                newDocument.Add(new Field("Owner", string.Empty, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            }

            _writer.AddDocument(newDocument);

            if (document == null)
            {
                _deletes.Add(n);
            }
        }

        public IndexReader OpenReader()
        {
            IndexReader reader = IndexReader.Open(_directory, false);

            foreach (int n in _deletes)
            {
                reader.DeleteDocument(n);
            }

            return reader;
        }
    }
}
