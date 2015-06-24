// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public interface IIndexReaderProcessorHandler
    {
        void Begin(IndexReader indexReader);
        void End(IndexReader indexReader);
        void Process(IndexReader indexReader, string readerName, int n, Document document, string id, NuGetVersion version);
    }
}
