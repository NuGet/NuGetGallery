using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery;
using System.Runtime.Versioning;
using System.Data.SqlClient;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO.Compression;

namespace NuGet.Indexing
{
    public static class PackageIndexing
    {
        const int MaxDocumentsPerCommit = 800;      //  The maximum number of Lucene documents in a single commit. The min size for a segment.
        const int MergeFactor = 10;                 //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this 

        public static TextWriter DefaultTraceWriter = TextWriter.Null;

        public static void CreateFreshIndex(Lucene.Net.Store.Directory directory)
        {
            CreateNewEmptyIndex(directory);
        }

        //  this function will incrementally build an index from the gallery using a high water mark stored in the commit metadata
        //  this function is useful for building a fresh index as in that case it is more efficient than diff-ing approach

        public static void RebuildIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory, FrameworksList frameworks, TextWriter log = null, PerfEventTracker perfTracker = null)
        {
            perfTracker = perfTracker ?? new PerfEventTracker();
            log = log ?? DefaultTraceWriter;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            using (perfTracker.TrackEvent("RebuildIndex", String.Empty))
            {
                // Empty the index, we're rebuilding
                CreateNewEmptyIndex(directory);

                var projectFxs = frameworks.Load();

                log.WriteLine("get curated feeds by PackageRegistration");
                IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(sqlConnectionString, log, verbose: false);

                int highestPackageKey = 0;
                while (true)
                {
                    log.WriteLine("get the checksums from the gallery");
                    IDictionary<int, int> checksums = GalleryExport.FetchGalleryChecksums(sqlConnectionString, highestPackageKey);

                    log.WriteLine("get packages from gallery where the Package.Key > {0}", highestPackageKey);
                    List<Package> packages = GalleryExport.GetPublishedPackagesSince(sqlConnectionString, highestPackageKey, log, verbose: false);

                    if (packages.Count == 0)
                    {
                        break;
                    }

                    log.WriteLine("associate the feeds and checksum data with each packages");
                    List<IndexDocumentData> indexDocumentData = MakeIndexDocumentData(packages, feeds, checksums);
                    highestPackageKey = indexDocumentData.Max(d => d.Package.Key);

                    AddPackagesToIndex(indexDocumentData, directory, log, projectFxs, perfTracker);

                    // Summarize performance
                    // (Save some time by not bothering if the log is "null")
                    if (!ReferenceEquals(TextWriter.Null, log) && !ReferenceEquals(PerfEventTracker.Null, perfTracker))
                    {
                        SummarizePerf(log, perfTracker);
                    }
                }
            }

            SummarizePerf(log, perfTracker);

            sw.Stop();
            log.WriteLine("all done, took {0}", sw.Elapsed);
        }

        private static void SummarizePerf(TextWriter log, PerfEventTracker perfTracker)
        {
            log.WriteLine("Perf Summary:");
            foreach (var evt in perfTracker.GetEvents())
            {
                var summary = perfTracker.GetSummary(evt);
                log.WriteLine(" {0} Avg:{5:0.00}ms Max: {1:0.00}ms({2}) Min: {3:0.00}ms({4})",
                    evt,
                    summary.Max.Duration.TotalMilliseconds,
                    summary.Max.Payload,
                    summary.Min.Duration.TotalMilliseconds,
                    summary.Min.Payload,
                    summary.Average.TotalMilliseconds);
            }
            perfTracker.Clear();
        }

        private static void AddPackagesToIndex(List<IndexDocumentData> indexDocumentData, Lucene.Net.Store.Directory directory, TextWriter log, IEnumerable<FrameworkName> projectFxs, PerfEventTracker perfTracker)
        {
            log.WriteLine("About to add {0} packages", indexDocumentData.Count);

            for (int index = 0; index < indexDocumentData.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, indexDocumentData.Count - index);

                List<IndexDocumentData> rangeToIndex = indexDocumentData.GetRange(index, count);

                AddToIndex(directory, rangeToIndex, log, projectFxs, perfTracker);
            }
        }

        private static void AddToIndex(Lucene.Net.Store.Directory directory, List<IndexDocumentData> rangeToIndex, TextWriter log, IEnumerable<FrameworkName> projectFxs, PerfEventTracker perfTracker)
        {
            log.WriteLine("begin AddToIndex");

            int highestPackageKey = -1;

            var groups = rangeToIndex.GroupBy(d => d.Package.PackageRegistration.Id).ToList();

            // Collect documents to change
            var dirtyDocs = new List<FacetedDocument>();
            using (var reader = IndexReader.Open(directory, readOnly: true))
            using (perfTracker.TrackEvent("CalculateChanges", ""))
            {
                foreach (var group in groups)
                {
                    var newDirtyDocs = DetermineDirtyDocuments(projectFxs, perfTracker, reader, group.Key, group);

                    // (Re-)Add any dirty documents to the index
                    dirtyDocs.AddRange(newDirtyDocs);
                }
            }

            using (IndexWriter indexWriter = CreateIndexWriter(directory, create: false))
            {
                WriteDirtyDocuments(dirtyDocs, indexWriter, perfTracker);

                highestPackageKey = rangeToIndex.Max(i => i.Package.Key);

                log.WriteLine("about to commit {0} packages", rangeToIndex.Count);

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                string lastEditsIndexTime = commitUserData["last-edits-index-time"];

                if (lastEditsIndexTime == null)
                {
                    //  this should never happen but if it did Lucene would throw 
                    lastEditsIndexTime = DateTime.MinValue.ToString();
                }

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(lastEditsIndexTime, highestPackageKey, rangeToIndex.Count, "add"));

                log.WriteLine("commit done");
            }

            log.WriteLine("end AddToIndex");
        }

        private static void WriteDirtyDocuments(List<FacetedDocument> dirtyDocs, IndexWriter indexWriter, PerfEventTracker perfTracker)
        {
            // Delete dirty documents and flush
            foreach (var dirtyDoc in dirtyDocs.Where(d => d.Dirty))
            {
                indexWriter.DeleteDocuments(dirtyDoc.GetQuery());
            }

            using (perfTracker.TrackEvent("FlushingDeletes", ""))
            {
                indexWriter.Flush(triggerMerge: false, flushDocStores: true, flushDeletes: true);
            }

            // (Re-)add dirty documents
            foreach (var dirtyDoc in dirtyDocs)
            {
                using (perfTracker.TrackEvent("AddDocument", "{0} v{1}", dirtyDoc.Id, dirtyDoc.Version))
                {
                    indexWriter.AddDocument(CreateLuceneDocument(dirtyDoc));
                }
            }
        }

        private static IEnumerable<FacetedDocument> DetermineDirtyDocuments(IEnumerable<FrameworkName> projectFxs, PerfEventTracker perfTracker, IndexReader reader, string id, IEnumerable<IndexDocumentData> data)
        {
            using (perfTracker.TrackEvent("Processdata", id))
            {
                // Get all documents matching the ID of this data.
                var documents = CollectExistingDocuments(perfTracker, reader, id);

                // Add the new documents
                using (perfTracker.TrackEvent("CreateNewDocuments", id))
                {
                    foreach (var package in data)
                    {
                        documents.Add(new FacetedDocument(package));
                    }
                }

                // Process the facets
                UpdateFacets(id, documents, projectFxs, perfTracker);

                return documents.Where(d => d.Dirty);
            }
        }

        private static List<FacetedDocument> CollectExistingDocuments(PerfEventTracker perfTracker, IndexReader reader, string id)
        {
            var docs = reader.TermDocs(new Term("Id", id.ToLowerInvariant()));
            var documents = new List<FacetedDocument>();
            using (perfTracker.TrackEvent("GetExistingDocuments", id))
            {
                while (docs.Next())
                {
                    documents.Add(new FacetedDocument(reader.Document(docs.Doc)));
                }
            }
            return documents;
        }

        private static void UpdateFacets(string packageId, IList<FacetedDocument> documents, IEnumerable<FrameworkName> projectFxs, PerfEventTracker perfTracker)
        {
            using (perfTracker.TrackEvent("UpdateFacets", "{0} ({1} items)", packageId, documents.Count))
            {
                // Collect all the current latest versions into dictionaries
                IDictionary<string, List<FacetedDocument>> existingFacets = new Dictionary<string, List<FacetedDocument>>(StringComparer.OrdinalIgnoreCase);

                using (perfTracker.TrackEvent("FindExistingFacets", packageId))
                {
                    foreach (var document in documents.Where(d => !d.IsNew))
                    {
                        foreach (var projectFx in projectFxs)
                        {
                            AddToExistingFacetsList(existingFacets, document, projectFx, Facets.LatestStableVersion(projectFx));
                            AddToExistingFacetsList(existingFacets, document, projectFx, Facets.LatestPrereleaseVersion(projectFx));
                        }
                    }
                }

                IDictionary<string, FacetedDocument> candidateNewFacets = new Dictionary<string, FacetedDocument>();

                // Process the new documents
                var newDocs = documents.Where(d => d.IsNew).OrderByDescending(d => d.Version).ToList();
                documents = null; // Done with the master list of all documents

                using (perfTracker.TrackEvent("DetermineNewLatestVersions", packageId))
                {
                    foreach (var doc in newDocs)
                    {
                        if (!String.IsNullOrEmpty(doc.Version.SpecialVersion))
                        {
                            doc.AddFacet(Facets.PrereleaseVersion);
                        }
                        if (doc.Data.Package.Listed)
                        {
                            doc.AddFacet(Facets.Listed);
                        }
                        var packageFxs = doc.Data.Package.SupportedFrameworks
                            .Select(fx =>
                            {
                                using (perfTracker.TrackEvent("ParseFrameworkName", fx.TargetFramework))
                                {
                                    return VersionUtility.ParseFrameworkName(fx.TargetFramework);
                                }
                            })
                            .ToList();

                        // Process each target framework
                        foreach (var projectFx in projectFxs)
                        {
                            if (projectFx == FrameworksList.AnyFramework || VersionUtility.IsCompatible(projectFx, packageFxs))
                            {
                                ProcessCompatibleVersion(packageId, perfTracker, candidateNewFacets, doc, projectFx);
                            }
                        }
                    }
                }

                // Adjust facets as needed
                using (perfTracker.TrackEvent("AdjustProjectFxes", packageId))
                {
                    foreach (var projectFx in projectFxs)
                    {
                        using (perfTracker.TrackEvent("AdjustProjectFx", "{0} ({1})", packageId, projectFx.FullName))
                        {
                            UpdateLatestVersionFacet(existingFacets, candidateNewFacets, Facets.LatestStableVersion(projectFx));
                            UpdateLatestVersionFacet(existingFacets, candidateNewFacets, Facets.LatestPrereleaseVersion(projectFx));
                        }
                    }
                }
            }
        }

        private static void ProcessCompatibleVersion(string packageId, PerfEventTracker perfTracker, IDictionary<string, FacetedDocument> candidateNewFacets, FacetedDocument doc, FrameworkName projectFx)
        {
            using (perfTracker.TrackEvent("ProcessCompatibleVersion", "{0} v{1} (fx:{2})", packageId, doc.Version, projectFx))
            {
                // Add compatible facet
                doc.AddFacet(Facets.Compatible(projectFx));

                // If listed, process it against latest versions
                if (doc.Data.Package.Listed)
                {
                    // Check it against the current latest prerelease and swap latests if necessary
                    string latestPreFacet = Facets.LatestPrereleaseVersion(projectFx);
                    string latestStableFacet = Facets.LatestStableVersion(projectFx);
                    if (!candidateNewFacets.ContainsKey(latestPreFacet))
                    {
                        candidateNewFacets[latestPreFacet] = doc;
                    }

                    // If this package is a stable version, do the same for latest stable
                    if (String.IsNullOrEmpty(doc.Version.SpecialVersion) && !candidateNewFacets.ContainsKey(latestStableFacet))
                    {
                        candidateNewFacets[latestStableFacet] = doc;
                    }
                }
            }
        }

        private static void AddToExistingFacetsList(IDictionary<string, List<FacetedDocument>> existingFacets, FacetedDocument document, FrameworkName projectFx, string facet)
        {
            if (document.HasFacet(facet))
            {
                List<FacetedDocument> existingList;
                if (!existingFacets.TryGetValue(facet, out existingList))
                {
                    existingList = new List<FacetedDocument>();
                    existingFacets[facet] = existingList;
                }
                existingList.Add(document);
            }
        }

        private static void UpdateLatestVersionFacet(IDictionary<string, List<FacetedDocument>> existingValuesByFacet, IDictionary<string, FacetedDocument> candidateNewValueByFacet, string facet)
        {
            FacetedDocument newValue;
            if (candidateNewValueByFacet.TryGetValue(facet, out newValue))
            {
                List<FacetedDocument> existingValues;
                FacetedDocument trueLatest = newValue;
                if (existingValuesByFacet.TryGetValue(facet, out existingValues))
                {
                    // Find the true latest
                    var oldLatest = existingValues.Where(d => d.Data.Package.Listed).OrderByDescending(d => d.Version).FirstOrDefault();
                    if (oldLatest != null && oldLatest.Version > trueLatest.Version)
                    {
                        trueLatest = oldLatest;
                    }

                    // Remove the facets from all the existing values, unless one of them happens to be the new one
                    foreach (var existing in existingValues)
                    {
                        if (existing != trueLatest)
                        {
                            existing.RemoveFacet(facet);
                        }
                    }
                }

                // Add the facet to the new value (this is idempotent, so it's ok if the document already has the facet)
                trueLatest.AddFacet(facet);
            }
        }

        private static void RemoveLatestFacets(string facet, IEnumerable<FacetedDocument> currentLatests)
        {
            foreach (var doc in currentLatests)
            {
                doc.RemoveFacet(facet);
            }
        }

        public static void CreateNewEmptyIndex(Lucene.Net.Store.Directory directory)
        {
            using (IndexWriter indexWriter = CreateIndexWriter(directory, true))
            {
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(DateTime.MinValue, 0, 0, "creation"));
            }
        }

        private static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory, bool create)
        {
            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.MergeFactor = MergeFactor;
            indexWriter.MaxMergeDocs = MaxMergeDocs;

            indexWriter.SetSimilarity(new CustomSimilarity());

            //StreamWriter streamWriter = new StreamWriter(Console.OpenStandardOutput());
            //indexWriter.SetInfoStream(streamWriter);
            //streamWriter.Flush();

            // this should theoretically work but appears to cause empty commit commitMetadata to not be saved
            //((LogMergePolicy)indexWriter.MergePolicy).SetUseCompoundFile(false);
            return indexWriter;
        }

        private static IDictionary<string, string> CreateCommitMetadata(DateTime lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            return CreateCommitMetadata(lastEditsIndexTime.ToString(), highestPackageKey, count, description);
        }

        private static IDictionary<string, string> CreateCommitMetadata(string lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();

            commitMetadata.Add("commit-time-stamp", DateTime.UtcNow.ToString());
            commitMetadata.Add("commit-description", description ?? string.Empty);
            commitMetadata.Add("commit-document-count", count.ToString());

            commitMetadata.Add("highest-package-key", highestPackageKey.ToString());
            commitMetadata.Add("last-edits-index-time", lastEditsIndexTime ?? DateTime.MinValue.ToString());

            commitMetadata.Add("MaxDocumentsPerCommit", MaxDocumentsPerCommit.ToString());
            commitMetadata.Add("MergeFactor", MergeFactor.ToString());
            commitMetadata.Add("MaxMergeDocs", MaxMergeDocs.ToString());

            return commitMetadata;
        }

        private static DateTime GetLastEditsIndexTime(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);

            string lastEditsIndexTime;
            if (commitMetadata.TryGetValue("last-edits-index-time", out lastEditsIndexTime))
            {
                return DateTime.Parse(lastEditsIndexTime);
            }

            return DateTime.MinValue;
        }

        private static int GetHighestPackageKey(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);

            string highestPackageKey;
            if (commitMetadata.TryGetValue("highest-package-key", out highestPackageKey))
            {
                return int.Parse(highestPackageKey);
            }

            return 0;
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
        private static Document CreateLuceneDocument(FacetedDocument documentData)
        {
            Package package = documentData.Data.Package;

            Document doc = new Document();

            //  Query Fields

            float titleBoost = 3.0f;
            float idBoost = 2.0f;

            if (package.Tags == null)
            {
                titleBoost += 0.5f;
                idBoost += 0.5f;
            }

            string title = package.Title ?? package.PackageRegistration.Id;

            Add(doc, "Id", package.PackageRegistration.Id, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "TokenizedId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "ShingledId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", package.Version, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", title, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, titleBoost);
            Add(doc, "Tags", package.Tags, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Description", package.Description, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Authors", package.FlattenedAuthors, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            foreach (User owner in package.PackageRegistration.Owners)
            {
                Add(doc, "Owners", owner.Username, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            }

            //  Sorting:

            doc.Add(new NumericField("PublishedDate", Field.Store.YES, true).SetIntValue(int.Parse(package.Published.ToString("yyyyMMdd"))));

            DateTime lastEdited = package.LastEdited ?? package.Published;
            doc.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEdited.ToString("yyyyMMdd"))));

            string displayName = String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            displayName = displayName.ToLower(CultureInfo.CurrentCulture);
            Add(doc, "DisplayName", displayName, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "IsLatest", package.IsLatest ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "IsLatestStable", package.IsLatestStable ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "Listed", package.Listed ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            if (documentData.Data.Feeds != null)
            {
                foreach (string feed in documentData.Data.Feeds)
                {
                    //  Store this to aid with debugging
                    Add(doc, "CuratedFeed", feed, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
                }
            }

            //  Add Package Key so we can quickly retrieve ranges of packages (in order to support the synchronization with the gallery)

            doc.Add(new NumericField("Key", Field.Store.YES, true).SetIntValue(package.Key));

            doc.Add(new NumericField("Checksum", Field.Store.YES, true).SetIntValue(documentData.Data.Checksum));

            //  Data we want to store in index - these cannot be queried

            JObject obj = PackageJson.ToJson(package);
            string data = obj.ToString(Formatting.None);

            Add(doc, "Data", data, Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            // Add facets
            foreach (var facet in documentData.DocFacets)
            {
                Add(doc, "Facet", facet, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            }

            doc.Boost = DetermineLanguageBoost(package.PackageRegistration.Id, package.Language);

            return doc;
        }

        public static void UpdateIndex(bool whatIf, List<int> adds, List<int> updates, List<int> deletes, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log, PerfEventTracker perfTracker, IEnumerable<FrameworkName> projectFxs)
        {
            log = log ?? DefaultTraceWriter;

            if (whatIf)
            {
                log.WriteLine("WhatIf mode");

                Apply(adds, keys => WhatIf_ApplyAdds(keys, fetch, directory, log));
                Apply(updates, keys => WhatIf_ApplyUpdates(keys, fetch, directory, log));
                Apply(deletes, keys => WhatIf_ApplyDeletes(keys, fetch, directory, log));
            }
            else
            {
                Apply(adds, keys => ApplyAdds(keys, fetch, directory, log, perfTracker, projectFxs));
                Apply(updates, keys => ApplyUpdates(keys, fetch, directory, log, perfTracker, projectFxs));
                Apply(deletes, keys => ApplyDeletes(keys, fetch, directory, log, perfTracker, projectFxs));
            }
        }

        private static void Apply(List<int> packageKeys, Action<List<int>> action)
        {
            for (int index = 0; index < packageKeys.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packageKeys.Count - index);
                List<int> range = packageKeys.GetRange(index, count);
                action(range);
            }
        }

        private static void WhatIf_ApplyAdds(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log)
        {
            log.WriteLine("[WhatIf] adding...");
            foreach (int packageKey in packageKeys)
            {
                IndexDocumentData documentData = fetch(packageKey);
                log.WriteLine("{0} {1} {2}", packageKey, documentData.Package.PackageRegistration.Id, documentData.Package.Version);
            }
        }

        private static void WhatIf_ApplyUpdates(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log)
        {
            log.WriteLine("[WhatIf] updating...");
            foreach (int packageKey in packageKeys)
            {
                IndexDocumentData documentData = fetch(packageKey);
                log.WriteLine("{0} {1} {2}", packageKey, documentData.Package.PackageRegistration.Id, documentData.Package.Version);
            }
        }

        private static void WhatIf_ApplyDeletes(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log)
        {
            log.WriteLine("[WhatIf] deleting...");
            foreach (int packageKey in packageKeys)
            {
                log.WriteLine("{0}", packageKey);
            }
        }

        private static void ApplyAdds(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log, PerfEventTracker perfTracker, IEnumerable<FrameworkName> projectFxs)
        {
            log.WriteLine("ApplyAdds");

            // Collect all the packages
            var packages = packageKeys.Select(k => fetch(k));

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                IDictionary<string, string> commitUserData;
                var dirtyDocuments = new List<FacetedDocument>();
                using (var reader = indexWriter.GetReader())
                {
                    commitUserData = reader.CommitUserData;
                    foreach (var group in packages.GroupBy(p => p.Package.PackageRegistration.Id))
                    {
                        var newDirtyDocs = DetermineDirtyDocuments(projectFxs, perfTracker, reader, group.Key, group);
                        dirtyDocuments.AddRange(newDirtyDocs);
                    }
                }

                WriteDirtyDocuments(dirtyDocuments, indexWriter, perfTracker);

                string lastEditsIndexTime = commitUserData["last-edits-index-time"];
                if (lastEditsIndexTime == null)
                {
                    //  this should never happen but if it did Lucene would throw 
                    lastEditsIndexTime = DateTime.MinValue.ToString();
                }

                log.WriteLine("Commit {0} adds", packageKeys.Count);
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(lastEditsIndexTime, packageKeys.Max(), packageKeys.Count, "add"));
            }
        }

        private static void ApplyUpdates(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log, PerfEventTracker perfTracker, IEnumerable<FrameworkName> projectFxs)
        {
            log.WriteLine("ApplyUpdates");

            PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

            // Collect all the packages
            var packages = packageKeys.Select(k => fetch(k));

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                var dirtyDocuments = new List<FacetedDocument>();
                IDictionary<string, string> commitUserData;
                using (var reader = indexWriter.GetReader())
                {
                    commitUserData = reader.CommitUserData;

                    // Group by Id
                    foreach (var group in packages.GroupBy(p => p.Package.PackageRegistration.Id))
                    {
                        // Collect existing documents
                        IList<FacetedDocument> existing = CollectExistingDocuments(perfTracker, indexWriter.GetReader(), group.Key).ToList();

                        // Replace the documents we need to replace
                        foreach (var package in group)
                        {
                            Query query = NumericRangeQuery.NewIntRange("Key", package.Package.Key, package.Package.Key, true, true);
                            indexWriter.DeleteDocuments(query);
                            var existingDoc = existing.FirstOrDefault(d => SemanticVersion.Parse(package.Package.NormalizedVersion).Equals(d.Version));
                            if (existingDoc != null)
                            {
                                existing.Remove(existingDoc);
                            }
                            existing.Add(new FacetedDocument(package));
                        }

                        // Recalculate facets
                        UpdateFacets(group.Key, existing, projectFxs, perfTracker);

                        // Add dirty documents
                        dirtyDocuments.AddRange(existing.Where(d => d.Dirty));
                    }
                }

                // Process dirty documents
                WriteDirtyDocuments(dirtyDocuments, indexWriter, perfTracker);

                commitUserData["count"] = packageKeys.Count.ToString();
                commitUserData["commit-description"] = "update";

                log.WriteLine("Commit {0} updates (delete and re-add)", packageKeys.Count);
                indexWriter.Commit(commitUserData);
            }
        }

        private static void ApplyDeletes(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory, TextWriter log, PerfEventTracker perfTracker, IEnumerable<FrameworkName> projectFxs)
        {
            log.WriteLine("ApplyDeletes");

            PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

            // Collect all the packages
            var packages = packageKeys.Select(k => fetch(k));

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                var dirtyDocuments = new List<FacetedDocument>();
                IDictionary<string, string> commitUserData;
                using (var reader = indexWriter.GetReader())
                {
                    commitUserData = reader.CommitUserData;
                    
                    // Group by Id
                    foreach (var group in packages.GroupBy(p => p.Package.PackageRegistration.Id))
                    {
                        // Collect existing documents
                        IEnumerable<FacetedDocument> existing = CollectExistingDocuments(perfTracker, indexWriter.GetReader(), group.Key);

                        // Remove the documents we need to remove
                        foreach (var package in group)
                        {
                            Query query = NumericRangeQuery.NewIntRange("Key", package.Package.Key, package.Package.Key, true, true);
                            indexWriter.DeleteDocuments(query);
                            existing = existing.Where(d =>
                                !SemanticVersion.Parse(package.Package.NormalizedVersion).Equals(d.Version));
                        }

                        // Recalculate facets
                        UpdateFacets(group.Key, existing.ToList(), projectFxs, perfTracker);

                        // Add dirty documents
                        dirtyDocuments.AddRange(existing.Where(d => d.Dirty));
                    }
                }

                // Process dirty documents
                WriteDirtyDocuments(dirtyDocuments, indexWriter, perfTracker);

                commitUserData["count"] = packageKeys.Count.ToString();
                commitUserData["commit-description"] = "delete";

                log.WriteLine("Commit {0} deletes", packageKeys.Count);
                indexWriter.Commit(commitUserData);
            }
        }

        //  helper functions

        public static IDictionary<int, IndexDocumentData> LoadDocumentData(string connectionString, List<int> adds, List<int> updates, List<int> deletes, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums, TextWriter log = null)
        {
            log = log ?? DefaultTraceWriter;

            IDictionary<int, IndexDocumentData> packages = new Dictionary<int, IndexDocumentData>();

            List<Package> addsPackages = GalleryExport.GetPackages(connectionString, adds, log, verbose: false);
            List<IndexDocumentData> addsIndexDocumentData = MakeIndexDocumentData(addsPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in addsIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            List<Package> updatesPackages = GalleryExport.GetPackages(connectionString, updates, log, verbose: false);
            List<IndexDocumentData> updatesIndexDocumentData = MakeIndexDocumentData(updatesPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in updatesIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            return packages;
        }

        public static List<IndexDocumentData> MakeIndexDocumentData(IList<Package> packages, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums)
        {
            Func<int, IEnumerable<string>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<string> ret = null;
                feeds.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            List<IndexDocumentData> result = packages
                .Select(p => new IndexDocumentData { Package = p, Checksum = checksums[p.Key], Feeds = GetFeeds(p.PackageRegistrationKey) })
                .ToList();

            return result;
        }
    }
}
