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
        const int MaxDocumentsPerCommit = 8000;     //  The maximum number of Lucene documents in a single commit. The min size for a segment.
        const int MergeFactor = 100;                //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this 

        public static TextWriter TraceWriter = Console.Out;

        public static void CreateFreshIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory, PackageRanking packageRanking)
        {
            CreateNewEmptyIndex(directory);

            DateTime indexTime = DateTime.UtcNow;

            TraceWriter.WriteLine("get all packages from gallery");
            List<Tuple<Package, IEnumerable<string>>> packages = GalleryExport.GetAllPackages(sqlConnectionString, indexTime);

            TraceWriter.WriteLine("get overall ranking from warehouse");
            IDictionary<string, int> overallRanking = packageRanking.GetOverallRanking();

            TraceWriter.WriteLine("get project ranking from warehouse");
            IDictionary<string, IDictionary<string, int>> projectRankings = packageRanking.GetProjectRankings();

            AddPackagesToIndex(packages, directory, overallRanking, projectRankings, indexTime);
        }

        public static void UpdateIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory, PackageRanking packageRanking)
        {
            DateTime? lastIndexTime = GetLastIndexTime(directory);

            if (lastIndexTime.HasValue)
            {
                DateTime indexTime = DateTime.UtcNow;

                TraceWriter.WriteLine("get recently edited packages from gallery");
                List<Tuple<Package, IEnumerable<string>>> editedPackages = GalleryExport.GetEditedPackagesSince(sqlConnectionString, indexTime, lastIndexTime.Value);

                TraceWriter.WriteLine("get recently added packages from gallery");
                List<Tuple<Package, IEnumerable<string>>> newPackages = GalleryExport.GetPackagesSince(sqlConnectionString, indexTime, lastIndexTime.Value);

                if (editedPackages.Count == 0 && newPackages.Count == 0)
                {
                    return;
                }

                //TODO: can we also get deletes

                TraceWriter.WriteLine("get overall ranking from warehouse");
                IDictionary<string, int> overallRanking = packageRanking.GetOverallRanking();

                TraceWriter.WriteLine("get project ranking from warehouse");
                IDictionary<string, IDictionary<string, int>> projectRankings = packageRanking.GetProjectRankings();

                UpdatePackagesInIndex(editedPackages, directory, overallRanking, projectRankings, indexTime);

                AddPackagesToIndex(newPackages, directory, overallRanking, projectRankings, indexTime);
            }
            else
            {
                CreateFreshIndex(sqlConnectionString, directory, packageRanking);
            }
        }

        private static void AddPackagesToIndex(List<Tuple<Package, IEnumerable<string>>> packages, Lucene.Net.Store.Directory directory, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, DateTime indexTime)
        {
            TraceWriter.WriteLine("About to add {0} packages", packages.Count);

            for (int index = 0; index < packages.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packages.Count - index);

                List<Tuple<Package, IEnumerable<string>>> rangeToIndex = packages.GetRange(index, count);

                AddToIndex(directory, rangeToIndex, overallRanking, projectRankings, indexTime);
            }
        }

        private static void UpdatePackagesInIndex(List<Tuple<Package, IEnumerable<string>>> packages, Lucene.Net.Store.Directory directory, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, DateTime indexTime)
        {
            TraceWriter.WriteLine("About to update {0} packages", packages.Count);

            for (int index = 0; index < packages.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, packages.Count - index);

                List<Tuple<Package, IEnumerable<string>>> rangeToIndex = packages.GetRange(index, count);

                UpdateInIndex(directory, rangeToIndex, overallRanking, projectRankings, indexTime);
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

        private static void AddToIndex(Lucene.Net.Store.Directory directory, IList<Tuple<Package, IEnumerable<string>>> packagesToIndex, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, DateTime indexTime)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.MergeFactor = MergeFactor;
                indexWriter.MaxMergeDocs = MaxMergeDocs; 

                foreach (Tuple<Package, IEnumerable<string>> packageToIndex in packagesToIndex)
                {
                    int rank = GetDocumentRank(overallRanking, packageToIndex.Item1.PackageRegistration.Id);
                    IDictionary<string, int> projectTypeRankings = PivotProjectTypeRanking(projectRankings, packageToIndex.Item1.PackageRegistration.Id);

                    Document newDocument = PackageIndexing.CreateLuceneDocument(packageToIndex.Item1, packageToIndex.Item2, rank, projectTypeRankings);

                    indexWriter.AddDocument(newDocument);
                }

                TraceWriter.WriteLine("about to commit {0} packages", packagesToIndex.Count);

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(indexTime, packagesToIndex.Count));
            }
        }

        private static void UpdateInIndex(Lucene.Net.Store.Directory directory, IList<Tuple<Package, IEnumerable<string>>> packagesToUpdate, IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings, DateTime indexTime)
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

                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(indexTime, packagesToUpdate.Count));
            }
        }

        public static void CreateNewEmptyIndex(Lucene.Net.Store.Directory directory)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), true, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(DateTime.UtcNow, 0));
            }
        }

        private static IDictionary<string, string> CreateCommitMetadata(DateTime indexTime, int count)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();
            commitMetadata.Add("last-index-time", indexTime.ToString());
            commitMetadata.Add("document-count", count.ToString());
            return commitMetadata;
        }

        private static DateTime? GetLastIndexTime(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);

            string lastIndexTime;
            if (commitMetadata.TryGetValue("last-index-time", out lastIndexTime))
            {
                return DateTime.Parse(lastIndexTime);
            }

            return null;
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
