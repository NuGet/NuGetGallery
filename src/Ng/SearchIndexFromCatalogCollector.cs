using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Ng
{
    public class SearchIndexFromCatalogCollector : CommitCollector
    {
        const int MergeFactor = 10;

        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
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
            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items);

            using (IndexWriter indexWriter = CreateIndexWriter(_directory))
            {
                Trace.TraceInformation("Index contains {0} documents", indexWriter.NumDocs());

                int count = 0;

                count += ProcessPackages(indexWriter, catalogItems);
                count += ProcessPackageDeletes(indexWriter, catalogItems);

                indexWriter.Commit(CreateCommitMetadata(commitTimeStamp));

                Trace.TraceInformation("COMMIT {0} documents, index contains {1} documents commitTimeStamp {2}", count, indexWriter.NumDocs(), commitTimeStamp.ToString("O"));
            }

            return true;
        }

        static async Task<IEnumerable<JObject>> FetchCatalogItems(CollectorHttpClient client, IEnumerable<JToken> items)
        {
            IList<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JToken item in items)
            {
                Uri catalogItemUri = item["@id"].ToObject<Uri>();

                tasks.Add(client.GetJObjectAsync(catalogItemUri));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(t => t.Result);
        }

        static int ProcessPackages(IndexWriter indexWriter, IEnumerable<JObject> catalogItems)
        {
            int i = 0;

            foreach (JObject catalogItem in catalogItems.Where(x => x["@type"].ToString().ToLowerInvariant().Contains("packagedetails")))
            {
                indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem["id"].ToString(), catalogItem["version"].ToString()));

                int publishedDate = 0;
                JToken publishedValue;

                if (catalogItem.TryGetValue("published", out publishedValue))
                {
                   publishedDate = int.Parse(publishedValue.ToObject<DateTime>().ToString("yyyyMMdd"));
                }
                              
                //Filter out unlisted packages
                if (publishedDate != 19000101)
                {
                    Document document = MakeDocument(catalogItem);
                    indexWriter.AddDocument(document);
                }

                i++;
            }

            return i;
        }

        static int ProcessPackageDeletes(IndexWriter indexWriter, IEnumerable<JObject> catalogItems)
        {
            int i = 0;

            foreach (JObject catalogItem in catalogItems.Where(x => x["@type"].ToString().ToLowerInvariant().Contains("packagedelete")))
            {
                indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem["id"].ToString(), catalogItem["version"].ToString()));

                i++;
            }

            return i;
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

        static Query CreateDeleteQuery(string id, string version)
        {
            //  note as we are not using the QueryParser we are not running this data through the analyzer so we need to mimic its behavior
            string analyzedId = id.ToLowerInvariant();
            string analyzedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            BooleanQuery query = new BooleanQuery();
            query.Add(new BooleanClause(new TermQuery(new Term("Id", analyzedId)), Occur.MUST));
            query.Add(new BooleanClause(new TermQuery(new Term("Version", analyzedVersion)), Occur.MUST));
            return query;
        }

        static Document MakeDocument(JObject catalogEntry)
        {
            string id = catalogEntry["id"].ToString();
            string version = catalogEntry["version"].ToString();

            string packageUrl = string.Format(_packageTemplate, id.ToLowerInvariant(), version.ToLowerInvariant());

            return CreateLuceneDocument(catalogEntry, packageUrl);
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

        static Document CreateLuceneDocument(JObject package, string packageUrl)
        {
            JToken type = package["@type"];

            //TODO: for now this is a MicroservicePackage hi-jack - later we can make this Docuemnt creation more generic
            if (Utils.IsType(package["@context"], package, Schema.DataTypes.ApiAppPackage))
            {
                return CreateLuceneDocument_ApiApp(package, packageUrl);
            }

            return CreateLuceneDocument_NuGet(package, packageUrl);
        }

        static Document CreateLuceneDocument_ApiApp(JObject package, string packageUrl)
        {
            Document doc = CreateLuceneDocument_Core(package, packageUrl);

            Add(doc, "Publisher", (string)package["publisher"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "@type", Schema.DataTypes.ApiAppPackage.AbsoluteUri, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            AddStoragePaths(doc, package, "https://nugetmspre.blob.core.windows.net/");

            //BUG BUG BUG : just use https
            AddStoragePaths(doc, package, "http://nugetmspre.blob.core.windows.net/");

            return doc;
        }

        static Document CreateLuceneDocument_NuGet(JObject package, string packageUrl)
        {
            Document doc = CreateLuceneDocument_Core(package, packageUrl);

            Add(doc, "@type", Schema.DataTypes.NuGetClassicPackage.AbsoluteUri, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            return doc;
        }

        static Document CreateLuceneDocument_Core(JObject package, string packageUrl)
        {
            Document doc = new Document();

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
            Add(doc, "IdAutocompletePhrase", "/ " + (string)package["id"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "TokenizedId", (string)package["id"], Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "ShingledId", (string)package["id"], Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", (string)package["version"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", title, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, titleBoost);
            Add(doc, "Tags", string.Join(" ", (package["tags"] ?? new JArray()).Select(s => (string)s)), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Description", (string)package["description"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Authors", string.Join(" ", (package["authors"] ?? new JArray()).Select(s => (string)s)), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Summary", (string)package["summary"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "IconUrl", (string)package["iconUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ProjectUrl", (string)package["projectUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "MinClientVersion", (string)package["minClientVersion"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ReleaseNotes", (string)package["releaseNotes"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Copyright", (string)package["copyright"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "LicenseUrl", (string)package["licenseUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "RequiresLicenseAcceptance", (string)package["requiresLicenseAcceptance"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "PackageSize", (string)package["packageSize"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Language", (string)package["language"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            Add(doc, "Namespace", (string)package["domain"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            Add(doc, "TenantId", (string)package["tenantId"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Visibility", (string)package["visibility"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            doc.Add(new NumericField("PublishedDate", Field.Store.YES, true).SetIntValue(int.Parse(package["published"].ToObject<DateTime>().ToString("yyyyMMdd"))));

            DateTime lastEdited = (DateTime)(package["lastEdited"] ?? package["published"]);
            doc.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEdited.ToString("yyyyMMdd"))));

            string displayName = String.IsNullOrEmpty((string)package["title"]) ? (string)package["id"] : (string)package["title"];
            displayName = displayName.ToLower(CultureInfo.CurrentCulture);
            Add(doc, "DisplayName", displayName, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "Url", packageUrl.ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            Add(doc, "PackageContent", (string)package["packageContent"], Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Add(doc, "CatalogEntry", (string)package["@id"], Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            doc.Boost = DetermineLanguageBoost((string)package["id"], (string)package["language"]);

            return doc;
        }

        static void AddStoragePaths(Document doc, JObject package, string baseAddress)
        {
            int len = baseAddress.Length;
            foreach (string storagePath in GetStoragePaths(package))
            {
                if (storagePath.StartsWith(baseAddress))
                {
                    string relativePath = storagePath.Substring(len);
                    doc.Add(new Field("StoragePath", relativePath, Field.Store.YES, Field.Index.NOT_ANALYZED));
                }
            }
        }

        static IEnumerable<string> GetStoragePaths(JObject package)
        {
            IList<string> storagePaths = new List<string>();
            storagePaths.Add(package["@id"].ToString());
            storagePaths.Add(package["packageContent"].ToString());
            foreach (JObject entry in package["entries"])
            {
                storagePaths.Add(entry["location"].ToString());
            }
            return storagePaths;
        }
    }
}
