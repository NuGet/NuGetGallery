using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    public class IndexPackageDeletesTask : IndexTask
    {
        public override void ExecuteCommand()
        {
            Lucene.Net.Store.Directory directory = GetDirectory();

            const int ChunkSize = 32000;

            int highWaterMark = 0;

            IList<int> packagesToDelete = new List<int>();

            while (true)
            {
                Tuple<int, int, HashSet<int>> result = GalleryExport.GetNextBlockOfPackageIds(ConnectionString.ToString(), highWaterMark, ChunkSize);

                int minPackageKey = result.Item1;
                int maxPackageKey = result.Item2;
                HashSet<int> databasePackageKeys = result.Item3;

                if (databasePackageKeys.Count == 0)
                {
                    break;
                }

                HashSet<int> indexPackageKeys = GetRangeFromIndex(minPackageKey, maxPackageKey, directory);

                FindInIndexButNotInDatabase(databasePackageKeys, indexPackageKeys, packagesToDelete);

                highWaterMark = maxPackageKey;
            }

            DeletePackageFromIndex(packagesToDelete, directory);
        }

        private static void DeletePackageFromIndex(IList<int> packagesToDelete, Lucene.Net.Store.Directory directory)
        {
            const int MergeFactor = 10;                 //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
            const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this 

            PackageQueryParser queryParser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Id", new PackageAnalyzer());

            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.MergeFactor = MergeFactor;
                indexWriter.MaxMergeDocs = MaxMergeDocs;

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                foreach (int packageKey in packagesToDelete)
                {
                    Query query = NumericRangeQuery.NewIntRange("Key", packageKey, packageKey, true, true);
                    indexWriter.DeleteDocuments(query);
                }

                commitUserData["count"] = packagesToDelete.Count.ToString();
                commitUserData["commit-description"] = "delete";

                indexWriter.Commit(commitUserData);
            }
        }

        private static void FindInIndexButNotInDatabase(HashSet<int> databasePackageKeys, HashSet<int> indexPackageKeys, IList<int> packagesToDelete)
        {
            foreach (int packageKey in indexPackageKeys)
            {
                if (!databasePackageKeys.Contains(packageKey))
                {
                    packagesToDelete.Add(packageKey);
                }
            }
        }

        private static HashSet<int> GetRangeFromIndex(int minPackageKey, int maxPackageKey, Lucene.Net.Store.Directory directory)
        {
            //TODO: provide implementation that makes a call to the SearchService for this JArray
            //TODO: the problem is loading the index from a remote blob store is very slow indeed. The SearchService already has it loaded.

            using (IndexSearcher searcher = new IndexSearcher(directory))
            {
                NumericRangeQuery<int> numericRangeQuery = NumericRangeQuery.NewIntRange("Key", minPackageKey, maxPackageKey, true, true);

                JArray packageKeys = new JArray();
                searcher.Search(numericRangeQuery, new KeyCollector(packageKeys));

                HashSet<int> uniquePackageKeys = new HashSet<int>();
                foreach (int packageKey in packageKeys)
                {
                    uniquePackageKeys.Add(packageKey);
                }

                return uniquePackageKeys;
            }
        }

    }
}
