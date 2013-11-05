using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    [Command("checkindex", "Compare Database and Index", AltName = "checkindex")]
    public class CheckIndexTask : DatabaseTask
    {
        [Option("The host of the SearchService", AltName = "Host")]
        public string Host { get; set; }

        public override void ExecuteCommand()
        {
            const int ChunkSize = 32000;

            int highWaterMark = 0;

            HashSet<int> accumlatedDatabase = new HashSet<int>();
            HashSet<int> accumlatedIndex = new HashSet<int>();

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

                highWaterMark = maxPackageKey;

                accumlatedDatabase.AddRange(databasePackageKeys);
                accumlatedIndex.AddRange(indexPackageKeys);
            }

            List<int> foundInDatabaseNotInIndex = new List<int>();
            List<int> foundInIndexNotInDatabase = new List<int>();

            Compare(accumlatedDatabase, accumlatedIndex, foundInDatabaseNotInIndex, foundInIndexNotInDatabase);

            Console.WriteLine("found in database but not in index:");
            foreach (int key in foundInDatabaseNotInIndex)
            {
                Console.WriteLine(key);
            }

            Console.WriteLine("found in index but not in database:");
            foreach (int key in foundInIndexNotInDatabase)
            {
                Console.WriteLine(key);
            }

            Console.WriteLine("summary:");
            Console.WriteLine("{0} packages found in database but not in index", foundInDatabaseNotInIndex.Count);
            Console.WriteLine("{0} packages found in index but not in database", foundInIndexNotInDatabase.Count);
        }


        private void Compare(
            HashSet<int> databasePackageKeys, 
            HashSet<int> indexPackageKeys,
            List<int> foundInDatabaseNotInIndex,
            List<int> foundInIndexNotInDatabase)
        {
            foreach (int key in databasePackageKeys)
            {
                if (!indexPackageKeys.Contains(key))
                {
                    foundInDatabaseNotInIndex.Add(key);
                }
            }

            foreach (int key in indexPackageKeys)
            {
                if (!databasePackageKeys.Contains(key))
                {
                    foundInIndexNotInDatabase.Add(key);
                }
            }
        }
    }
}
