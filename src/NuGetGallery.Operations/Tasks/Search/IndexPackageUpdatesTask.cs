using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    [Command("indexpackageupdates", "Index Package Updates Task", AltName = "indexupdates")]
    public class IndexPackageUpdatesTask : IndexTask
    {
        [Option("A service running against the index to be updated", AltName = "host")]
        public string Host { get; set; }

        public override void ExecuteCommand()
        {
            IDictionary<int, int> database = GalleryExport.FetchGalleryChecksums(ConnectionString.ToString());

            Console.WriteLine("fetched {0} keys from database", database.Count);

            Tuple<int, int> minMax = GalleryExport.FindMinMaxKey(database);

            Console.WriteLine("min = {0}, max = {1}", minMax.Item1, minMax.Item2);

            IDictionary<int, int> index = SearchServiceClient.GetRangeFromIndex(minMax.Item1, minMax.Item2, Host);

            Console.WriteLine("fetched {0} keys from index", index.Count);

            List<int> adds = new List<int>();
            List<int> updates = new List<int>();
            List<int> deletes = new List<int>();

            SortIntoAddsUpdateDeletes(database, index, adds, updates, deletes);

            Console.WriteLine("{0} adds", adds.Count);
            Console.WriteLine("{0} updates", updates.Count);
            Console.WriteLine("{0} deletes", deletes.Count);

            if (adds.Count == 0 && updates.Count == 0)
            {
                return;
            }

            //  context

            PackageRanking packageRanking = new WarehousePackageRanking(StorageAccount);
            IndexDocumentContext context = new IndexDocumentContext(packageRanking.GetOverallRanking(), packageRanking.GetProjectRankings());

            //  data

            IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(ConnectionString.ToString());
            IDictionary<int, IndexDocumentData> packages = PackageIndexing.LoadDocumentData(ConnectionString.ToString(), adds, updates, deletes, feeds, database);

            Lucene.Net.Store.Directory directory = GetDirectory();

            PackageIndexing.UpdateIndex(WhatIf, adds, updates, deletes, (key) => { return packages[key]; }, context, directory);
        }

        private void SortIntoAddsUpdateDeletes(IDictionary<int, int> database, IDictionary<int, int> index, List<int> adds, List<int> updates, List<int> deletes)
        {
            foreach (KeyValuePair<int, int> databaseItem in database)
            {
                int indexChecksum = 0;
                if (index.TryGetValue(databaseItem.Key, out indexChecksum))
                {
                    if (databaseItem.Value != indexChecksum)
                    {
                        updates.Add(databaseItem.Key);
                    }
                }
                else
                {
                    adds.Add(databaseItem.Key);
                }
            }

            foreach (KeyValuePair<int, int> indexItem in index)
            {
                if (!database.ContainsKey(indexItem.Key))
                {
                    deletes.Add(indexItem.Key);
                }
            }
        }
    }
}
