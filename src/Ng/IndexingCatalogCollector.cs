// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ng
{
    //TODO: this is test code

    class IndexingCatalogCollector : CommitCollector
    {
        Lucene.Net.Store.Directory _directory;

        public IndexingCatalogCollector(Uri index, Lucene.Net.Store.Directory directory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _directory = directory;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            PerFieldAnalyzerWrapper analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30));
            analyzer.AddAnalyzer("Id", new IdentifierKeywordAnalyzer());

            int i = 0;

            using (IndexWriter writer = new IndexWriter(_directory, analyzer, false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (JObject item in items)
                {
                    i++;

                    string id = item["nuget:id"].ToString();
                    string version = item["nuget:version"].ToString();

                    BooleanQuery query = new BooleanQuery();
                    query.Add(new BooleanClause(new TermQuery(new Term("Id", id.ToLowerInvariant())), Occur.MUST));
                    query.Add(new BooleanClause(new TermQuery(new Term("Version", version)), Occur.MUST));

                    writer.DeleteDocuments(query);

                    Document doc = new Document();

                    doc.Add(new Field("Id", item["nuget:id"].ToString(), Field.Store.YES, Field.Index.ANALYZED));
                    doc.Add(new Field("Version", item["nuget:version"].ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

                    writer.AddDocument(doc);
                }

                string trace = Guid.NewGuid().ToString();

                writer.Commit(new Dictionary<string, string> 
                { 
                    { "commitTimeStamp", commitTimeStamp.ToString("O") },
                    { "trace", trace }
                });

                Trace.TraceInformation("COMMIT {0} documents, index contains {1} documents, commitTimeStamp {2}, trace: {3}",
                    i, writer.NumDocs(), commitTimeStamp.ToString("O"), trace);
            }

            return Task.FromResult(true);
        }
    }
}
