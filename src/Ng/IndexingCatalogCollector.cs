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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    //TODO: this is test code

    class IndexingCatalogCollector : BatchCollector
    {
        Lucene.Net.Store.Directory _directory;

        public IndexingCatalogCollector(Uri index, Lucene.Net.Store.Directory directory, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc, batchSize)
        {
            _directory = directory;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            //bool create = !IndexReader.IndexExists(_directory);

            //foreach (JObject item in items)
            //{
            //    Console.WriteLine("{0} {1}", item["nuget:id"], item["nuget:version"]);
            //}

            PerFieldAnalyzerWrapper analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30));
            analyzer.AddAnalyzer("Id", new IdentifierKeywordAnalyzer());

            using (IndexWriter writer = new IndexWriter(_directory, analyzer, false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (JObject item in items)
                {
                    string id = item["nuget:id"].ToString();
                    string version = item["nuget:version"].ToString();

                    BooleanQuery query = new BooleanQuery();
                    query.Add(new BooleanClause(new TermQuery(new Term("Id", id.ToLowerInvariant())), Occur.MUST));
                    query.Add(new BooleanClause(new TermQuery(new Term("Version", version)), Occur.MUST));

                    writer.DeleteDocuments(query);

                    //writer.Flush(triggerMerge: false, flushDocStores: true, flushDeletes: true);

                    Document doc = new Document();

                    doc.Add(new Field("Id", item["nuget:id"].ToString(), Field.Store.YES, Field.Index.ANALYZED));
                    doc.Add(new Field("Version", item["nuget:version"].ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

                    writer.AddDocument(doc);
                }

                writer.Commit();
            }

            return Task.FromResult(true);
        }
    }
}
