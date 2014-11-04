using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    public class SearchIndexFromCatalogCollector: CommitCollector
    {
        const int MergeFactor = 10;                 //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this

        Lucene.Net.Store.Directory _directory;
        const string _packageTemplate = "{0}/{1}.json";

        static Dictionary<string, string> _frameworkNames = new Dictionary<string, string>();

        public SearchIndexFromCatalogCollector(Uri index, Lucene.Net.Store.Directory directory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _directory = directory;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            Task<Document>[] packages = items.Select(x => MakePackage(client, x)).Where(x => x != null).ToArray();

            await Task.WhenAll(packages);

            using (IndexWriter indexWriter = CreateIndexWriter(_directory))
            {
                Trace.TraceInformation("Index contains {0} documents", indexWriter.NumDocs());

                int i = 0;

                foreach (Document doc in packages.Select(x => x.Result))
                {
                    if (doc != null)
                    {
                        string id = doc.Get("Id");
                        string version = doc.Get("Version");

                        indexWriter.DeleteDocuments(CreateDeleteQuery(id, version));

                        indexWriter.AddDocument(doc);

                        Trace.TraceInformation("ADD: {0}/{1}", id, version);

                        i++;
                    }
                }

                indexWriter.Commit(CreateCommitMetadata(commitTimeStamp));

                Trace.TraceInformation("COMMIT {0} documents, index contains {1} documents commitTimeStamp {2}", i, indexWriter.NumDocs(), commitTimeStamp.ToString("O"));
            }

            return true;
        }

        IDictionary<string, string> CreateCommitMetadata(DateTime commitTimeStamp)
        {
            return new Dictionary<string, string> { { "commitTimeStamp", commitTimeStamp.ToString("O") } };
        }

        internal static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory)
        {
            bool create = !IndexReader.IndexExists(directory);

            directory.EnsureOpen();

            if (!create)
            {
                if (IndexWriter.IsLocked(directory))
                {
                    IndexWriter.Unlock(directory);
                }
            }

            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.MergeFactor = MergeFactor;
            indexWriter.MaxMergeDocs = MaxMergeDocs;

            indexWriter.SetSimilarity(new CustomSimilarity());

            return indexWriter;
        }

        Query CreateDeleteQuery(string id, string version)
        {
            //  note as we are not using the QueryParser we are not running this data through the analyzer so we need to mimic its behavior
            id = id.ToLowerInvariant();

            BooleanQuery query = new BooleanQuery();
            query.Add(new BooleanClause(new TermQuery(new Term("Id", id)), Occur.MUST));
            query.Add(new BooleanClause(new TermQuery(new Term("Version", version)), Occur.MUST));
            return query;
        }

        private async Task<Document> MakePackage(CollectorHttpClient client, JToken catalogEntry)
        {
            string resultString = await client.GetStringAsync((string)catalogEntry["@id"]);
            JObject result = JObject.Parse(resultString);

            string id = (string)result["id"];
            string version = (string)result["version"];

            string packageUrl = string.Format(_packageTemplate, id.ToLowerInvariant(), version.ToLowerInvariant());

            return CreateLuceneDocument(result, packageUrl);
        }

        private static void Add(Document doc, string name, string value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            if (value == null)
            {
                return;
            }

            Field newField = new Field(name, value, store, index, termVector);
            newField.Boost = boost;
            doc.Add(newField);
        }

        private static void Add(Document doc, string name, int value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            Add(doc, name, value.ToString(CultureInfo.InvariantCulture), store, index, termVector, boost);
        }

        private static float DetermineLanguageBoost(string id, string language)
        {
            if (!string.IsNullOrWhiteSpace(language))
            {
                string languageSuffix = "." + language.Trim();
                if (id.EndsWith(languageSuffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    return 0.1f;
                }
            }
            return 1.0f;
        }

        // ----------------------------------------------------------------------------------------------------------------------------------------
        private static Document CreateLuceneDocument(JObject package, string packageUrl)
        {
            Document doc = new Document();

            if (((IDictionary<string, JToken>)package).ContainsKey("supportedFrameworks"))
            {
                foreach (JToken fwk in package["supportedFrameworks"])
                {
                    string framework = (string)fwk;

                    FrameworkName frameworkName = VersionUtility.ParseFrameworkName(framework);

                    lock (_frameworkNames)
                    {
                        if (!_frameworkNames.ContainsKey(framework))
                        {
                            _frameworkNames.Add(framework, frameworkName.FullName);
                            
                            Trace.TraceInformation("New framework string: {0}, {1}", framework, frameworkName.FullName);
                            
                            using (var writer = File.AppendText("frameworks.txt"))
                            {
                                writer.WriteLine("{0}: {1}", framework, frameworkName.FullName);
                            }
                        }
                    }
                    Add(doc, "TargetFramework", framework == "any" ? "any" : frameworkName.ToString(), Field.Store.YES /* NO */, Field.Index.NO, Field.TermVector.NO);
                }
            }

            //  Query Fields

            float titleBoost = 3.0f;
            float idBoost = 2.0f;

            if (package["tags"] == null)
            {
                titleBoost += 0.5f;
                idBoost += 0.5f;
            }

            string title = (string)(package["title"] ?? package["id"]);

            Add(doc, "Id", (string)package["id"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "IdAutocomplete", (string)package["id"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO);
            Add(doc, "TokenizedId", (string)package["id"], Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "ShingledId", (string)package["id"], Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", (string)package["version"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", title, Field.Store.YES /* NO */, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, titleBoost);
            Add(doc, "Tags", string.Join(", ", (package["tags"] ?? new JArray()).Select(s => (string)s)), Field.Store.YES /* NO */, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Description", (string)package["description"], Field.Store.YES /* NO */, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Authors", string.Join(", ", package["authors"].Select(s => (string)s)), Field.Store.YES /* NO */, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            doc.Add(new NumericField("PublishedDate", Field.Store.YES, true).SetIntValue(int.Parse(package["published"].ToObject<DateTime>().ToString("yyyyMMdd"))));

            DateTime lastEdited = (DateTime)(package["lastEdited"] ?? package["published"]);
            doc.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEdited.ToString("yyyyMMdd"))));

            string displayName = String.IsNullOrEmpty((string)package["title"]) ? (string)package["id"] : (string)package["title"];
            displayName = displayName.ToLower(CultureInfo.CurrentCulture);
            Add(doc, "DisplayName", displayName, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "Url", packageUrl.ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            doc.Boost = DetermineLanguageBoost((string)package["id"], (string)package["language"]);

            return doc;
        }
    }
}
