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

        public static void CreateFreshIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory, PackageRanking packageRanking)
        {
            CreateNewEmptyIndex(directory);
        }

        public static void IncrementallyUpdateIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory, PackageRanking packageRanking)
        {
            TraceWriter.WriteLine("get overall ranking from warehouse");
            IDictionary<string, int> overallRanking = packageRanking.GetOverallRanking();

            TraceWriter.WriteLine("get project ranking from warehouse");
            IDictionary<string, IDictionary<string, int>> projectRankings = packageRanking.GetProjectRankings();

            while (true)
            {
                TraceWriter.WriteLine("get curated feeds by PackageRegistration");
                IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(sqlConnectionString);

                DateTime indexTime = DateTime.UtcNow;
                int highestPackageKey = GetHighestPackageKey(directory);

                TraceWriter.WriteLine("indexTime = {0} mostRecentPublished = {1}", indexTime, highestPackageKey);

                TraceWriter.WriteLine("get packages from gallery where the Package.Key > {0}", highestPackageKey);
                List<Package> packages = GalleryExport.GetPublishedPackagesSince(sqlConnectionString, highestPackageKey);

                if (packages.Count == 0)
                {
                    break;
                }

                TraceWriter.WriteLine("associate the feeds with gallery");
                List<Tuple<Package, IEnumerable<string>>> packagesWithFeeds = AssociatedFeedsWithPackages(packages, feeds);

                AddPackagesToIndex(packagesWithFeeds, directory, overallRanking, projectRankings);
            }

            TraceWriter.WriteLine("all done");
        }

        public static void ApplyPackageEdits(string sqlConnectionString, Lucene.Net.Store.Directory directory, PackageRanking packageRanking)
        {
            TraceWriter.WriteLine("get overall ranking from warehouse");
            IDictionary<string, int> overallRanking = packageRanking.GetOverallRanking();

            TraceWriter.WriteLine("get project ranking from warehouse");
            IDictionary<string, IDictionary<string, int>> projectRankings = packageRanking.GetProjectRankings();

            while (true)
            {
                TraceWriter.WriteLine("get curated feeds by PackageRegistration");
                IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(sqlConnectionString);

                int highestPackageKey = GetHighestPackageKey(directory);
                DateTime lastEditsIndexTime = GetLastEditsIndexTime(directory);

                TraceWriter.WriteLine("get edited packages from gallery since {0} where the Package.Key less than {1}", lastEditsIndexTime, highestPackageKey);
                List<Package> packages = GalleryExport.GetEditedPackagesSince(sqlConnectionString, highestPackageKey, lastEditsIndexTime);

                if (packages.Count == 0)
                {
                    break;
                }

                foreach (Package package in packages)
                {
                    TraceWriter.WriteLine(package.Key);
                }

                TraceWriter.WriteLine("associate the feeds with gallery");
                List<Tuple<Package, IEnumerable<string>>> packagesWithFeeds = AssociatedFeedsWithPackages(packages, feeds);

                UpdatePackagesInIndex(packagesWithFeeds, directory, overallRanking, projectRankings, highestPackageKey, DateTime.UtcNow);
            }

            TraceWriter.WriteLine("all done");
        }

        private static List<Tuple<Package, IEnumerable<string>>> AssociatedFeedsWithPackages(IList<Package> packages, IDictionary<int, IEnumerable<string>> feeds)
        {
            Func<int, IEnumerable<string>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<string> ret = null;
                feeds.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            List<Tuple<Package, IEnumerable<string>>> packagesAndFeeds = packages
                .Select(p => new Tuple<Package, IEnumerable<string>>(p, GetFeeds(p.PackageRegistrationKey)))
                .ToList();

            return packagesAndFeeds;
        }

        private static void AddPackagesToIndex(List<Tuple<Package, IEnumerable<string>>> packages, Lucene.Net.Store.Directory directory, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings)
        {
            TraceWriter.WriteLine("About to add {0} packages", packages.Count);

            for (int index = 0; index < packages.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packages.Count - index);

                List<Tuple<Package, IEnumerable<string>>> rangeToIndex = packages.GetRange(index, count);

                AddToIndex(directory, rangeToIndex, overallRanking, projectRankings);
            }
        }

        private static void UpdatePackagesInIndex(List<Tuple<Package, IEnumerable<string>>> packages, Lucene.Net.Store.Directory directory, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, int highestPackageKey, DateTime indexTime)
        {
            TraceWriter.WriteLine("About to update {0} packages", packages.Count);

            for (int index = 0; index < packages.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packages.Count - index);

                List<Tuple<Package, IEnumerable<string>>> rangeToIndex = packages.GetRange(index, count);

                UpdateInIndex(directory, rangeToIndex, overallRanking, projectRankings, highestPackageKey, indexTime);
            }
        }

        private static int GetDocumentRank(IDictionary<string, int> rank, string packageId)
        {
            int val;
            if (rank.TryGetValue(packageId, out val))
            {
                return val;
            }
            return 100000;
        }

        private static IDictionary<string, int> PivotProjectTypeRanking(IDictionary<string, IDictionary<string, int>> rankings, string packageId)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();
            foreach (KeyValuePair<string, IDictionary<string, int>> ranking in rankings)
            {
                int rank;
                if (ranking.Value.TryGetValue(packageId, out rank))
                {
                    result.Add(ranking.Key, rank);
                }
            }
            return result;
        }

        private static void AddToIndex(Lucene.Net.Store.Directory directory, IList<Tuple<Package, IEnumerable<string>>> packagesToIndex, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.MergeFactor = MergeFactor;
                indexWriter.MaxMergeDocs = MaxMergeDocs;

                int highestPackageKey = -1;

                foreach (Tuple<Package, IEnumerable<string>> packageToIndex in packagesToIndex)
                {
                    int currentPackageKey = packageToIndex.Item1.Key;
                    
                    int rank = GetDocumentRank(overallRanking, packageToIndex.Item1.PackageRegistration.Id);
                    IDictionary<string, int> projectTypeRankings = PivotProjectTypeRanking(projectRankings, packageToIndex.Item1.PackageRegistration.Id);

                    Document newDocument = PackageIndexing.CreateLuceneDocument(packageToIndex.Item1, packageToIndex.Item2, rank, projectTypeRankings);

                    indexWriter.AddDocument(newDocument);

                    if (currentPackageKey <= highestPackageKey)
                    {
                        throw new Exception("(currentPackageKey <= highestPackageKey) the data must not be ordered correctly");
                    }
                    
                    highestPackageKey = currentPackageKey;
                }

                TraceWriter.WriteLine("about to commit {0} packages", packagesToIndex.Count);

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(commitUserData["last-edits-index-time"], highestPackageKey, packagesToIndex.Count, "publish"));
            }
        }

        private static void UpdateInIndex(Lucene.Net.Store.Directory directory, IList<Tuple<Package, IEnumerable<string>>> packagesToUpdate, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, int highestPackageKey, DateTime indexTime)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.MergeFactor = MergeFactor;
                indexWriter.MaxMergeDocs = MaxMergeDocs;

                PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

                foreach (Tuple<Package, IEnumerable<string>> packageToUpdate in packagesToUpdate)
                {
                    int rank = GetDocumentRank(overallRanking, packageToUpdate.Item1.PackageRegistration.Id);
                    IDictionary<string, int> projectTypeRankings = PivotProjectTypeRanking(projectRankings, packageToUpdate.Item1.PackageRegistration.Id);

                    Document newDocument = PackageIndexing.CreateLuceneDocument(packageToUpdate.Item1, packageToUpdate.Item2, rank, projectTypeRankings);

                    string id = packageToUpdate.Item1.PackageRegistration.Id;
                    string version = packageToUpdate.Item1.Version;

                    //  using the QueryParser will cause the custom Analyzers to be used in creating the query
                    Query query = queryParser.Parse(string.Format("+Id:{0} +Version:{1}", id, version));

                    TraceWriter.WriteLine("about to delete with query {0}", query.ToString());

                    indexWriter.DeleteDocuments(query);

                    indexWriter.AddDocument(newDocument);
                }

                TraceWriter.WriteLine("about to commit {0} packages", packagesToUpdate.Count);

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(indexTime, highestPackageKey, packagesToUpdate.Count, "update"));
            }
        }

        public static Tuple<IEnumerable<int>, IEnumerable<int>> DifferenceInRange(Lucene.Net.Store.Directory directory, HashSet<int> packageKeys)
        {
            //  (1) find min and max packageKeys 
            //  (2) execute range query on index
            //  (3) if result size from index == hashset size then we are good - return Tuple(new in[], new int[]>)
            //  (4) else iterate over index result to determine what is in hashset etc.
            //  (5) build lists of missing from hashset and missing from index

            //  Note: general idea is that packages have been deleted in db (and therefore hashset) but are still in index

            return new Tuple<IEnumerable<int>,IEnumerable<int>>(new int[0], new int[0]);
        }

        public static void CreateNewEmptyIndex(Lucene.Net.Store.Directory directory)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), true, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(DateTime.MinValue, 0, 0, "creation"));
            }
        }

        private static IDictionary<string, string> CreateCommitMetadata(DateTime lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            return CreateCommitMetadata(lastEditsIndexTime.ToString(), highestPackageKey, count, description);
        }

        private static IDictionary<string, string> CreateCommitMetadata(string lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();

            commitMetadata.Add("commit-time-stamp",  DateTime.UtcNow.ToString());
            commitMetadata.Add("commit-description", description);
            commitMetadata.Add("commit-document-count", count.ToString());

            commitMetadata.Add("highest-package-key", highestPackageKey.ToString());
            commitMetadata.Add("last-edits-index-time", lastEditsIndexTime);

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

        private static Document CreateLuceneDocument(Package package, IEnumerable<string> feeds, int rank, IDictionary<string, int> projectTypeRankings)
        {
            Document doc = new Document();

            //  Query Fields

            float idBoost = (package.Title != null) ? 1.5f : 3.0f;

            Add(doc, "Id", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "TokenizedId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", package.Version, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", package.Title, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
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

            if (feeds != null)
            {
                foreach (string feed in feeds)
                {
                    Add(doc, "CuratedFeed", feed, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
                }
            }

            foreach (PackageFramework packageFramework in package.SupportedFrameworks)
            {
                Add(doc, "SupportedFramework", packageFramework.TargetFramework, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            }

            //  Add ranking fields

            doc.Add(new NumericField("Rank", Field.Store.YES, true).SetIntValue(rank));

            foreach (KeyValuePair<string, int> projectTypeRanking in projectTypeRankings)
            {
                doc.Add(new NumericField(projectTypeRanking.Key, Field.Store.YES, true).SetIntValue(projectTypeRanking.Value));
            }

            //  Add Package Key so we can quickly retrieve ranges of packages

            doc.Add(new NumericField("Key", Field.Store.YES, true).SetIntValue(package.Key));

            //  Data we wnat to store in index - these cannot be queried

            JObject obj = PackageJson.ToJson(package);

            //  add these to help with debugging boosting

            obj.Add("Rank", rank);
            foreach (KeyValuePair<string, int> item in projectTypeRankings)
            {
                obj.Add(item.Key, item.Value);
            }

            string data = obj.ToString();

            Add(doc, "Data", data, Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            return doc;
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
    }
}
