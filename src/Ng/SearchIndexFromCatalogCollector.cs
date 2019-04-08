// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Ng.Extensions;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace Ng
{
    public class SearchIndexFromCatalogCollector : CommitCollector
    {
        private readonly string _baseAddress;
        private readonly Uri _galleryBaseAddress;

        private readonly IndexWriter _indexWriter;
        private readonly bool _commitEachBatch;
        private readonly TimeSpan? _commitTimeout;
        private readonly ILogger _logger;

        private LuceneCommitMetadata _metadataForNextCommit;

        public SearchIndexFromCatalogCollector(
            Uri index,
            IndexWriter indexWriter,
            bool commitEachBatch,
            TimeSpan? commitTimeout,
            string baseAddress,
            Uri galleryBaseAddress,
            ITelemetryService telemetryService,
            ILogger logger,
            Func<HttpMessageHandler> handlerFunc = null,
            IHttpRetryStrategy httpRetryStrategy = null)
            : base(index, telemetryService, handlerFunc, httpRetryStrategy: httpRetryStrategy)
        {
            _indexWriter = indexWriter;
            _commitEachBatch = commitEachBatch;
            _commitTimeout = commitTimeout;
            _baseAddress = baseAddress;
            _galleryBaseAddress = galleryBaseAddress;
            _logger = logger;
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            JObject catalogIndex = null;
            if (_baseAddress != null)
            {
                var stopwatch = Stopwatch.StartNew();
                catalogIndex = await client.GetJObjectAsync(Index, cancellationToken);
                _telemetryService.TrackCatalogIndexReadDuration(stopwatch.Elapsed, Index);
            }

            IEnumerable<JObject> catalogItems = await FetchCatalogItemsAsync(client, items, cancellationToken);

            var numDocs = _indexWriter.NumDocs();
            _logger.LogInformation(string.Format("Index contains {0} documents.", _indexWriter.NumDocs()));

            ProcessCatalogIndex(_indexWriter, catalogIndex, _baseAddress);
            ProcessCatalogItems(_indexWriter, catalogItems, _baseAddress);

            var docsDifference = _indexWriter.NumDocs() - numDocs;

            UpdateCommitMetadata(commitTimeStamp, docsDifference);

            _logger.LogInformation(string.Format("Processed catalog items. Index now contains {0} documents. (total uncommitted {1}, batch {2})",
                _indexWriter.NumDocs(), _metadataForNextCommit.Count, docsDifference));

            if (_commitEachBatch || isLastBatch)
            {
                await EnsureCommittedAsync();
            }

            return true;
        }

        private void UpdateCommitMetadata(DateTime commitTimeStamp, int docsDifference)
        {
            var count = docsDifference;
            if (_metadataForNextCommit != null)
            {
                // we want the total for the entire commit, so add to the number we already have
                count += _metadataForNextCommit.Count;
            }

            _metadataForNextCommit = DocumentCreator.CreateCommitMetadata(
                commitTimeStamp, "from catalog", count, Guid.NewGuid().ToString());
        }

        public async Task EnsureCommittedAsync()
        {
            if (_metadataForNextCommit == null)
            {
                // this means no changes have been made to the index - no need to commit
                _logger.LogInformation(string.Format("SKIP COMMIT No changes. Index contains {0} documents.", _indexWriter.NumDocs()));
                return;
            }

            try
            {
                var commitTask = CommitIndexAsync();

                // Ensure that the commit finishes within the configured timeout. If the timeout
                // threshold is reached, an OperationCanceledException will be thrown and will cause
                // the process to crash. The process MUST be ended as otherwise the commit task may
                // finish in the background.
                if (_commitTimeout.HasValue)
                {
                    commitTask = commitTask.TimeoutAfter(_commitTimeout.Value);
                }

                await commitTask;
            }
            catch (OperationCanceledException)
            {
                _telemetryService.TrackIndexCommitTimeout();
                _logger.LogError("TIMEOUT Committing index containing {0} documents. Metadata: commitTimeStamp {CommitTimeStamp}; change count {ChangeCount}; trace {CommitTrace}",
                    _indexWriter.NumDocs(), _metadataForNextCommit.CommitTimeStamp.ToString("O"), _metadataForNextCommit.Count, _metadataForNextCommit.Trace);

                Environment.Exit(exitCode: 1);
            }
        }

        private async Task CommitIndexAsync()
        {
            // This method commits to the index synchronously and may hang. Yield the current context to allow cancellation.
            await Task.Yield();

            using (_telemetryService.TrackIndexCommitDuration())
            {
                _logger.LogInformation("COMMITTING index contains {0} documents. Metadata: commitTimeStamp {CommitTimeStamp}; change count {ChangeCount}; trace {CommitTrace}",
                    _indexWriter.NumDocs(), _metadataForNextCommit.CommitTimeStamp.ToString("O"), _metadataForNextCommit.Count, _metadataForNextCommit.Trace);

                _indexWriter.ExpungeDeletes();
                _indexWriter.Commit(_metadataForNextCommit.ToDictionary());

                _logger.LogInformation("COMMIT index contains {0} documents. Metadata: commitTimeStamp {CommitTimeStamp}; change count {ChangeCount}; trace {CommitTrace}",
                    _indexWriter.NumDocs(), _metadataForNextCommit.CommitTimeStamp.ToString("O"), _metadataForNextCommit.Count, _metadataForNextCommit.Trace);

                _metadataForNextCommit = null;
            }
        }

        private static async Task<IEnumerable<JObject>> FetchCatalogItemsAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<JObject>>();

            foreach (var item in items)
            {
                tasks.Add(client.GetJObjectAsync(item.Uri, cancellationToken));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(t => t.Result);
        }

        private static void ProcessCatalogIndex(IndexWriter indexWriter, JObject catalogIndex, string baseAddress)
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

        private void ProcessCatalogItems(IndexWriter indexWriter, IEnumerable<JObject> catalogItems, string baseAddress)
        {
            int count = 0;

            foreach (JObject catalogItem in catalogItems)
            {
                _logger.LogInformation("Process CatalogItem {CatalogItem}", catalogItem["@id"].ToString());

                NormalizeId(catalogItem);

                if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDetails))
                {
                    var properties = GetTelemetryProperties(catalogItem);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDetailsSeconds, properties))
                    {
                        ProcessPackageDetails(indexWriter, catalogItem);
                    }
                }
                else if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDelete))
                {
                    var properties = GetTelemetryProperties(catalogItem);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDeleteSeconds, properties))
                    {
                        ProcessPackageDelete(indexWriter, catalogItem);
                    }
                }
                else
                {
                    _logger.LogInformation("Unrecognized @type ignoring CatalogItem");
                }

                count++;
            }

            _logger.LogInformation(string.Format("Processed {0} CatalogItems", count));
        }

        private static Dictionary<string, string> GetTelemetryProperties(JObject catalogItem)
        {
            var packageId = catalogItem["id"].ToString().ToLowerInvariant();
            var packageVersion = catalogItem["version"].ToString().ToLowerInvariant();

            return new Dictionary<string, string>()
            {
                { TelemetryConstants.Id, packageId },
                { TelemetryConstants.Version, packageVersion }
            };
        }

        private static void NormalizeId(JObject catalogItem)
        {
            // for now, for apiapps, we have prepended the id in the catalog with the namespace, however we don't want this to impact the Lucene index
            JToken originalId = catalogItem["originalId"];
            if (originalId != null)
            {
                catalogItem["id"] = originalId.ToString();
            }
        }

        private static JToken GetContext(JObject catalogItem)
        {
            return catalogItem["@context"];
        }

        private void ProcessPackageDetails(IndexWriter indexWriter, JObject catalogItem)
        {
            _logger.LogDebug("ProcessPackageDetails");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));

            var package = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogItem, _galleryBaseAddress);
            var document = DocumentCreator.CreateDocument(package);
            indexWriter.AddDocument(document);
        }

        private void ProcessPackageDelete(IndexWriter indexWriter, JObject catalogItem)
        {
            _logger.LogDebug("ProcessPackageDelete");

            indexWriter.DeleteDocuments(CreateDeleteQuery(catalogItem));
        }

        private static Query CreateDeleteQuery(JObject catalogItem)
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

        private static void AddStoragePaths(Document doc, IEnumerable<string> storagePaths, string baseAddress)
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

        private static IEnumerable<string> GetStoragePaths(JObject package)
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

        private static IEnumerable<string> GetCatalogStoragePaths(JObject index)
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