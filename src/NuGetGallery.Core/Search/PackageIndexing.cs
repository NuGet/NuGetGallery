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

namespace NuGetGallery
{
    public static class PackageIndexing
    {
        const int MaxDocumentsPerCommit = 800;      //  The maximum number of Lucene documents in a single commit. The min size for a segment.
        const int MergeFactor = 10;                 //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this 

        public static TextWriter TraceWriter = Console.Out;

        public static void CreateFreshIndex(Lucene.Net.Store.Directory directory)
        {
            CreateNewEmptyIndex(directory);
        }

        //  this function will incrementally build an index from the gallery using a high water mark stored in the commit metadata
        //  this function is useful for building a fresh index as in that case it is more efficient than diff-ing approach

        public static void BuildIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory)
        {
            while (true)
            {
                DateTime indexTime = DateTime.UtcNow;
                int highestPackageKey = GetHighestPackageKey(directory);

                TraceWriter.WriteLine("get the checksums from the gallery");
                IDictionary<int, int> checksums = GalleryExport.FetchGalleryChecksums(sqlConnectionString, highestPackageKey);

                TraceWriter.WriteLine("get curated feeds by PackageRegistration");
                IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(sqlConnectionString);

                TraceWriter.WriteLine("indexTime = {0} mostRecentPublished = {1}", indexTime, highestPackageKey);

                TraceWriter.WriteLine("get packages from gallery where the Package.Key > {0}", highestPackageKey);
                List<Package> packages = GalleryExport.GetPublishedPackagesSince(sqlConnectionString, highestPackageKey);

                if (packages.Count == 0)
                {
                    break;
                }

                TraceWriter.WriteLine("associate the feeds and checksum data with each packages");
                List<IndexDocumentData> indexDocumentData = MakeIndexDocumentData(packages, feeds, checksums);

                AddPackagesToIndex(indexDocumentData, directory);
            }

            TraceWriter.WriteLine("all done");
        }

        private static void AddPackagesToIndex(List<IndexDocumentData> indexDocumentData, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("About to add {0} packages", indexDocumentData.Count);

            for (int index = 0; index < indexDocumentData.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, indexDocumentData.Count - index);

                List<IndexDocumentData> rangeToIndex = indexDocumentData.GetRange(index, count);

                AddToIndex(directory, rangeToIndex);
            }
        }

        private static void AddToIndex(Lucene.Net.Store.Directory directory, List<IndexDocumentData> rangeToIndex)
        {
            TraceWriter.WriteLine("begin AddToIndex");

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                int highestPackageKey = -1;

                foreach (IndexDocumentData documentData in rangeToIndex)
                {
                    int currentPackageKey = documentData.Package.Key;

                    Document newDocument = CreateLuceneDocument(documentData);

                    indexWriter.AddDocument(newDocument);

                    if (currentPackageKey <= highestPackageKey)
                    {
                        throw new Exception("(currentPackageKey <= highestPackageKey) the data must not be ordered correctly");
                    }
                    
                    highestPackageKey = currentPackageKey;
                }

                TraceWriter.WriteLine("about to commit {0} packages", rangeToIndex.Count);

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                string lastEditsIndexTime = commitUserData["last-edits-index-time"];

                if (lastEditsIndexTime == null)
                {
                    //  this should never happen but if it did Lucene would throw 
                    lastEditsIndexTime = DateTime.MinValue.ToString();
                }

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(lastEditsIndexTime, highestPackageKey, rangeToIndex.Count, "add"));

                TraceWriter.WriteLine("commit done");
            }

            TraceWriter.WriteLine("end AddToIndex");
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

            commitMetadata.Add("commit-time-stamp",  DateTime.UtcNow.ToString());
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

        // ----------------------------------------------------------------------------------------------------------------------------------------

        private static Document CreateLuceneDocument(IndexDocumentData documentData)
        {
            Package package = documentData.Package;

            Document doc = new Document();

            //  Query Fields

            float titleBoost = 3.0f;
            float idBoost = 2.0f;

            if (package.Title == null)
            {
                idBoost += titleBoost;
            }

            Add(doc, "Id", package.PackageRegistration.Id, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "TokenizedId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "ShingledId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", package.Version, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", package.Title, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, titleBoost);
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

            //  Facets:

            Add(doc, "IsLatest", package.IsLatest ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "IsLatestStable", package.IsLatestStable ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "Listed", package.Listed ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            if (documentData.Feeds != null)
            {
                foreach (string feed in documentData.Feeds)
                {
                    //  Store this to aid with debugging
                    Add(doc, "CuratedFeed", feed, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
                }
            }

            foreach (PackageFramework packageFramework in package.SupportedFrameworks)
            {
                Add(doc, "SupportedFramework", packageFramework.TargetFramework, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            }

            //  Add Package Key so we can quickly retrieve ranges of packages (in order to support the synchronization with the gallery)

            doc.Add(new NumericField("Key", Field.Store.YES, true).SetIntValue(package.Key));

            doc.Add(new NumericField("Checksum", Field.Store.YES, true).SetIntValue(documentData.Checksum));

            //  Data we want to store in index - these cannot be queried

            JObject obj = PackageJson.ToJson(package);
            string data = obj.ToString();

            Add(doc, "Data", data, Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            return doc;
        }

        public static void UpdateIndex(bool whatIf, List<int> adds, List<int> updates, List<int> deletes, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            if (whatIf)
            {
                TraceWriter.WriteLine("WhatIf mode");

                Apply(adds, WhatIf_ApplyAdds, fetch, directory);
                Apply(updates, WhatIf_ApplyUpdates, fetch, directory);
                Apply(deletes, WhatIf_ApplyDeletes, fetch, directory);
            }
            else
            {
                Apply(adds, ApplyAdds, fetch, directory);
                Apply(updates, ApplyUpdates, fetch, directory);
                Apply(deletes, ApplyDeletes, fetch, directory);
            }
        }

        private static void Apply(List<int> packageKeys, Action<List<int>, Func<int, IndexDocumentData>, Lucene.Net.Store.Directory> action, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            for (int index = 0; index < packageKeys.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packageKeys.Count - index);
                List<int> range = packageKeys.GetRange(index, count);
                action(range, fetch, directory);
            }
        }

        private static void WhatIf_ApplyAdds(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("[WhatIf] adding...");
            foreach (int packageKey in packageKeys)
            {
                IndexDocumentData documentData = fetch(packageKey);
                TraceWriter.WriteLine("{0} {1} {2}", packageKey, documentData.Package.PackageRegistration.Id, documentData.Package.Version);
            }
        }

        private static void WhatIf_ApplyUpdates(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("[WhatIf] updating...");
            foreach (int packageKey in packageKeys)
            {
                IndexDocumentData documentData = fetch(packageKey);
                TraceWriter.WriteLine("{0} {1} {2}", packageKey, documentData.Package.PackageRegistration.Id, documentData.Package.Version);
            }
        }

        private static void WhatIf_ApplyDeletes(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("[WhatIf] deleting...");
            foreach (int packageKey in packageKeys)
            {
                TraceWriter.WriteLine("{0}", packageKey);
            }
        }
        
        private static void ApplyAdds(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("ApplyAdds");

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                int highestPackageKey = -1;
                foreach (int packageKey in packageKeys)
                {
                    IndexDocumentData documentData = fetch(packageKey);
                    int currentPackageKey = documentData.Package.Key;
                    Document newDocument = CreateLuceneDocument(documentData);
                    indexWriter.AddDocument(newDocument);
                    if (currentPackageKey <= highestPackageKey)
                    {
                        throw new Exception("(currentPackageKey <= highestPackageKey) the data must not be ordered correctly");
                    }
                    highestPackageKey = currentPackageKey;
                }

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;
                string lastEditsIndexTime = commitUserData["last-edits-index-time"];
                if (lastEditsIndexTime == null)
                {
                    //  this should never happen but if it did Lucene would throw 
                    lastEditsIndexTime = DateTime.MinValue.ToString();
                }

                TraceWriter.WriteLine("Commit {0} adds", packageKeys.Count);
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(lastEditsIndexTime, highestPackageKey, packageKeys.Count, "add"));
            }
        }

        private static void ApplyUpdates(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("ApplyUpdates");

            PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                foreach (int packageKey in packageKeys)
                {
                    IndexDocumentData documentData = fetch(packageKey);

                    Query query = NumericRangeQuery.NewIntRange("Key", packageKey, packageKey, true, true);
                    indexWriter.DeleteDocuments(query);

                    Document newDocument = PackageIndexing.CreateLuceneDocument(documentData);
                    indexWriter.AddDocument(newDocument);
                }

                commitUserData["count"] = packageKeys.Count.ToString();
                commitUserData["commit-description"] = "update";

                TraceWriter.WriteLine("Commit {0} updates (delete and re-add)", packageKeys.Count);
                indexWriter.Commit(commitUserData);
            }
        }

        private static void ApplyDeletes(List<int> packageKeys, Func<int, IndexDocumentData> fetch, Lucene.Net.Store.Directory directory)
        {
            TraceWriter.WriteLine("ApplyDeletes");

            PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

            using (IndexWriter indexWriter = CreateIndexWriter(directory, false))
            {
                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                foreach (int packageKey in packageKeys)
                {
                    Query query = NumericRangeQuery.NewIntRange("Key", packageKey, packageKey, true, true);
                    indexWriter.DeleteDocuments(query);
                }

                commitUserData["count"] = packageKeys.Count.ToString();
                commitUserData["commit-description"] = "delete";

                TraceWriter.WriteLine("Commit {0} deletes", packageKeys.Count);
                indexWriter.Commit(commitUserData);
            }
        }

        //  helper functions

        public static IDictionary<int, IndexDocumentData> LoadDocumentData(string connectionString, List<int> adds, List<int> updates, List<int> deletes, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums)
        {
            IDictionary<int, IndexDocumentData> packages = new Dictionary<int, IndexDocumentData>();

            List<Package> addsPackages = GalleryExport.GetPackages(connectionString, adds);
            List<IndexDocumentData> addsIndexDocumentData = MakeIndexDocumentData(addsPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in addsIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            List<Package> updatesPackages = GalleryExport.GetPackages(connectionString, updates);
            List<IndexDocumentData> updatesIndexDocumentData = MakeIndexDocumentData(updatesPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in updatesIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            return packages;
        }

        private static List<IndexDocumentData> MakeIndexDocumentData(IList<Package> packages, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums)
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
