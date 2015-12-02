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
using System.Threading;
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

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            JObject catalogIndex = (_baseAddress != null) ? await client.GetJObjectAsync(Index, cancellationToken) : null;
            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items, cancellationToken);

            using (IndexWriter indexWriter = CreateIndexWriter(_directory))
            {
                Trace.TraceInformation("Index contains {0} documents", indexWriter.NumDocs());

                ProcessCatalogIndex(indexWriter, catalogIndex, _baseAddress);
                ProcessCatalogItems(indexWriter, catalogItems, _baseAddress);

                indexWriter.ExpungeDeletes();

                indexWriter.Commit(DocumentCreator.CreateCommitMetadata(commitTimeStamp, "from catalog", Guid.NewGuid().ToString()));

                Trace.TraceInformation("COMMIT index contains {0} documents commitTimeStamp {1}", indexWriter.NumDocs(), commitTimeStamp.ToString("O"));
            }

            return true;
        }

        static async Task<IEnumerable<JObject>> FetchCatalogItems(CollectorHttpClient client, IEnumerable<JToken> items, CancellationToken cancellationToken)
        {
            IList<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JToken item in items)
            {
                Uri catalogItemUri = item["@id"].ToObject<Uri>();

                tasks.Add(client.GetJObjectAsync(catalogItemUri, cancellationToken));
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
                    ProcessPackageDetails(indexWriter, catalogItem);
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

        static void ProcessPackageDetails(IndexWriter indexWriter, JObject catalogItem)
        {
            Trace.TraceInformation("ProcessPackageDetails");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));

            var package = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogItem);
            var document = DocumentCreator.CreateDocument(package);
            indexWriter.AddDocument(document);
        }

        static void ProcessPackageDelete(IndexWriter indexWriter, JObject catalogItem)
        {
            Trace.TraceInformation("ProcessPackageDelete");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));
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
