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
    //  note the delete task talks to the Database and the SearchService and the Index that sits behind the SerachService
    //  it reads from the SearchService to save itself from having to load the whole index.

    [Command("indexpackagedeletes", "Index Package Deletes Task", AltName = "indexdeletes")]
    public class IndexPackageDeletesTask : IndexTask
    {
        [Option("The host of the SearchService", AltName = "Host")]
        public string Host { get; set; }

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

                HashSet<int> indexPackageKeys = SearchServiceClient.GetRangeFromIndex(minPackageKey, maxPackageKey, Host);

                FindInIndexButNotInDatabase(databasePackageKeys, indexPackageKeys, packagesToDelete);

                highWaterMark = maxPackageKey;
            }

            PackageIndexing.DeletePackageFromIndex(packagesToDelete, directory);
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
    }
}
