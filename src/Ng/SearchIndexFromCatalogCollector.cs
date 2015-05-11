// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ng
{
    public class SearchIndexFromCatalogCollector : CommitCollector
    {
        private const string _packageTemplate = "{0}/{1}.json";
        private const int MergeFactor = 10;

        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        private const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this

        private readonly Lucene.Net.Store.Directory _directory;
        private readonly string _baseAddress;
        

        static Dictionary<string, string> _frameworkNames = new Dictionary<string, string>();

        public SearchIndexFromCatalogCollector(Uri index, Lucene.Net.Store.Directory directory, string baseAddress, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _directory = directory;
            _baseAddress = baseAddress;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            JObject catalogIndex = (_baseAddress != null) ? await client.GetJObjectAsync(Index) : null;
            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items);

            using (IndexWriter indexWriter = CreateIndexWriter(_directory))
            {
                Trace.TraceInformation("Index contains {0} documents", indexWriter.NumDocs());

                ProcessCatalogIndex(indexWriter, catalogIndex, _baseAddress);
                ProcessCatalogItems(indexWriter, catalogItems, _baseAddress);

                indexWriter.ExpungeDeletes();

                indexWriter.Commit(CreateCommitMetadata(commitTimeStamp));

                Trace.TraceInformation("COMMIT index contains {0} documents commitTimeStamp {1}", indexWriter.NumDocs(), commitTimeStamp.ToString("O"));
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

        static void ProcessCatalogIndex(IndexWriter indexWriter, JObject catalogIndex, string baseAddress)
        {
            indexWriter.DeleteDocuments(new Term("@type", Schema.DataTypes.CatalogInfastructure.AbsoluteUri));

            Document doc = new Document();

            Add(doc, "@type", Schema.DataTypes.CatalogInfastructure.AbsoluteUri, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Visibility", "Public", Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            if (catalogIndex != null)
            {
                IEnumerable<string> storagePaths = GetCatalogStoragePaths(catalogIndex);
                AddStoragePaths(doc, storagePaths, baseAddress);
            }

            indexWriter.AddDocument(doc);
        }

        static void ProcessCatalogItems(IndexWriter indexWriter, IEnumerable<JObject> catalogItems, string baseAddress)
        {
            int count = 0;

            foreach (JObject catalogItem in catalogItems)
            {
                Trace.TraceInformation("Process CatalogItem {0}", catalogItem["@id"]);

                NormalizeId(catalogItem);

                if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDetails))
                {
                    ProcessPackageDetails(indexWriter, catalogItem, baseAddress);
                }
                else if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDelete))
                {
                    ProcessPackageDelete(indexWriter, catalogItem);
                }
                else
                {
                    Trace.TraceInformation("Unrecognized @type ignoring CatalogItem");
                }

                count++;
            }

            Trace.TraceInformation("Processed {0} CatalogItems", count);
        }

        static void NormalizeId(JObject catalogItem)
        {
            // for now, for apiapps, we have prepended the id in the catalog with the namespace, however we don't want this to impact the Lucene index
            JToken originalId = catalogItem["originalId"];
            if (originalId != null)
            {
                catalogItem["id"] = originalId.ToString();
            }
        }

        static JToken GetContext(JObject catalogItem)
        {
            return catalogItem["@context"];
        }

        static void ProcessPackageDetails(IndexWriter indexWriter, JObject catalogItem, string baseAddress)
        {
            Trace.TraceInformation("ProcessPackageDetails");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));

            if (IsListed(catalogItem))
            {
                Document document = MakeDocument(catalogItem, baseAddress);
                indexWriter.AddDocument(document);
            }
        }

        static bool IsListed(JObject catalogItem)
        {
            JToken publishedValue;
            if (catalogItem.TryGetValue("published", out publishedValue))
            {
                var publishedDate = int.Parse(publishedValue.ToObject<DateTime>().ToString("yyyyMMdd"));
                return (publishedDate != 19000101);
            }
            else
            {
                return true;
            }
        }

        static void ProcessPackageDelete(IndexWriter indexWriter, JObject catalogItem)
        {
            Trace.TraceInformation("ProcessPackageDelete");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));
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

        static Query CreateDeleteQuery(JObject catalogItem)
        {
            string id = catalogItem["id"].ToString();
            string version = catalogItem["version"].ToString();

            //  note as we are not using the QueryParser we are not running this data through the analyzer so we need to mimic its behavior
            string analyzedId = id.ToLowerInvariant();
            string analyzedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            JToken nsJToken;
            if (catalogItem.TryGetValue("namespace", out nsJToken))
            {
                string ns = nsJToken.ToString();

                BooleanQuery query = new BooleanQuery();
                query.Add(new BooleanClause(new TermQuery(new Term("Id", analyzedId)), Occur.MUST));
                query.Add(new BooleanClause(new TermQuery(new Term("Version", analyzedVersion)), Occur.MUST));
                query.Add(new BooleanClause(new TermQuery(new Term("Namespace", ns)), Occur.MUST));
                return query;
            }
            else
            {
                BooleanQuery query = new BooleanQuery();
                query.Add(new BooleanClause(new TermQuery(new Term("Id", analyzedId)), Occur.MUST));
                query.Add(new BooleanClause(new TermQuery(new Term("Version", analyzedVersion)), Occur.MUST));
                return query;
            }
        }

        static Document MakeDocument(JObject catalogEntry, string baseAddress)
        {
            string id = catalogEntry["id"].ToString();
            string version = catalogEntry["version"].ToString();

            string packageUrl = string.Format(_packageTemplate, id.ToLowerInvariant(), version.ToLowerInvariant());

            return CreateLuceneDocument(catalogEntry, packageUrl, baseAddress);
        }

        static void Add(Document doc, string name, string value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            if (value == null)
            {
                return;
            }

            Field newField = new Field(name, value, store, index, termVector);
            newField.Boost = boost;
            doc.Add(newField);
        }

        static void Add(Document doc, string name, int value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            Add(doc, name, value.ToString(CultureInfo.InvariantCulture), store, index, termVector, boost);
        }

        static float DetermineLanguageBoost(string id, string language)
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

        static Document CreateLuceneDocument(JObject catalogEntry, string packageUrl, string baseAddress)
        {
            JToken type = catalogEntry["@type"];

            //TODO: for now this is a MicroservicePackage hi-jack - later we can make this Docuemnt creation more generic
            if (Utils.IsType(catalogEntry["@context"], catalogEntry, Schema.DataTypes.ApiAppPackage))
            {
                NormalizeId(catalogEntry);

                return CreateLuceneDocument_ApiApp(catalogEntry, packageUrl, baseAddress);
            }

            return CreateLuceneDocument_NuGet(catalogEntry, packageUrl);
        }

        static Document CreateLuceneDocument_ApiApp(JObject package, string packageUrl, string baseAddress)
        {
            Document doc = CreateLuceneDocument_Core(package, packageUrl);

            Add(doc, "Publisher", (string)package["publisher"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "@type", Schema.DataTypes.ApiAppPackage.AbsoluteUri, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            if (baseAddress != null)
            {
                IEnumerable<string> storagePaths = GetStoragePaths(package);
                AddStoragePaths(doc, storagePaths, baseAddress);
            }

            JToken owner = package["owner"];
            if (owner != null)
            {
                Add(doc, "Owner", (string)owner["nameIdentifier"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
                Add(doc, "OwnerDetails", owner.ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            }

            JToken license = package["license"];
            if (license != null)
            {
                Add(doc, "LicenseDetails", license.ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            }

            Add(doc, "Homepage", (string)package["homepage"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Categories", string.Join(" ", (package["categories"] ?? new JArray()).Select(s => (string)s)), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            JArray entries = package["entries"] as JArray;
            if (entries != null)
            {
                var smallIcon = entries.FirstOrDefault(e => e["@type"] is JArray && ((JArray)e["@type"]).Any(t => (string)t == "SmallIcon"));
                if (smallIcon != null)
                {
                    Add(doc, "IconUrl", (string)smallIcon["location"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
                }
            }

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

            Add(doc, "@type", Schema.DataTypes.Package.AbsoluteUri, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

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

            JToken authors = package["authors"] ?? package["author"];
            if (authors is JArray || authors == null)
            {
                Add(doc, "Authors", string.Join(" ", (authors ?? new JArray()).Select(s => (string) s)), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            }
            else
            {
                Add(doc, "Authors", (string)authors, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            }

            Add(doc, "Summary", (string)package["summary"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "IconUrl", (string)package["iconUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ProjectUrl", (string)package["projectUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "MinClientVersion", (string)package["minClientVersion"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ReleaseNotes", (string)package["releaseNotes"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Copyright", (string)package["copyright"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            
            Add(doc, "LicenseUrl", (string)package["licenseUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "RequiresLicenseAcceptance", (string)package["requiresLicenseAcceptance"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            
            Add(doc, "PackageHash", (string)package["packageHash"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "PackageHashAlgorithm", (string)package["packageHashAlgorithm"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "PackageSize", (string)package["packageSize"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            
            Add(doc, "Language", (string)package["language"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            string fullId = (package["namespace"] != null) ? string.Format("{0}.{1}", package["namespace"], package["id"]) : (string)package["id"];
            Add(doc, "FullId", fullId, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Namespace", (string)package["namespace"], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

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

            Add(doc, "Listed", (string)package["listed"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            //  The following fields are added for back compatibility with the V2 gallery

            Add(doc, "OriginalVersion", (string)package["verbatimVersion"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalCreated", (string)package["created"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalLastEdited", (string)package["lastEdited"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalPublished", (string)package["published"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "LicenseNames", (string)package["licenseNames"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "LicenseReportUrl", (string)package["licenseReportUrl"], Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            //TODO: add dependency summary

            doc.Boost = DetermineLanguageBoost((string)package["id"], (string)package["language"]);

            return doc;
        }
        
        static void AddStoragePaths(Document doc, IEnumerable<string> storagePaths, string baseAddress)
        {
            int len = baseAddress.Length;
            foreach (string storagePath in storagePaths)
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

        static IEnumerable<string> GetCatalogStoragePaths(JObject index)
        {
            IList<string> storagePaths = new List<string>();
            storagePaths.Add(index["@id"].ToString());
            foreach (JObject page in index["items"])
            {
                storagePaths.Add(page["@id"].ToString());
            }
            return storagePaths;
        }
    }
}
