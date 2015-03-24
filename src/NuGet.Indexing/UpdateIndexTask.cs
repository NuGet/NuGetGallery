using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    /// <summary>
    /// Updates the index incrementally.
    /// </summary>
    public class UpdateIndexTask : IndexTask
    {
        public override void Execute()
        {
            var manager = GetSearcherManager();
            IDictionary<int, int> databaseChecksums = GalleryExport.FetchGalleryChecksums(SqlConnectionString);

            Log.WriteLine("fetched {0} keys from database", databaseChecksums.Count);

            Tuple<int, int> minMax = GalleryExport.FindMinMaxKey(databaseChecksums);

            Log.WriteLine("min = {0}, max = {1}", minMax.Item1, minMax.Item2);

            // For now, use the in-memory Searcher client. But eventually this will use the original Search Service call below
            IDictionary<int, int> index = ParseRangeResult(
                Searcher.KeyRangeQuery(manager, minMax.Item1, minMax.Item2));
            
            Log.WriteLine("fetched {0} keys from index", index.Count);

            List<int> adds = new List<int>();
            List<int> updates = new List<int>();
            List<int> deletes = new List<int>();

            SortIntoAddsUpdateDeletes(databaseChecksums, index, adds, updates, deletes);

            Log.WriteLine("{0} adds", adds.Count);
            Log.WriteLine("{0} updates", updates.Count);
            Log.WriteLine("{0} deletes", deletes.Count);

            if (adds.Count == 0 && updates.Count == 0)
            {
                return;
            }

            IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(SqlConnectionString, Log, verbose: false);
            IDictionary<int, IndexDocumentData> packages = PackageIndexing.LoadDocumentData(SqlConnectionString, adds, updates, deletes, feeds, databaseChecksums, Log);

            Lucene.Net.Store.Directory directory = manager.Directory;

            Func<int, IndexDocumentData> packageFetcher = (key) =>
            {
                IndexDocumentData knownDoc;
                if (packages.TryGetValue(key, out knownDoc))
                {
                    return knownDoc;
                }
                else
                {
                    // We're modifying a different document
                    var pkgs = GalleryExport.GetPackages(SqlConnectionString, new List<int>() { key }, Log, verbose: false);
                    var docs = PackageIndexing.MakeIndexDocumentData(pkgs, feeds, databaseChecksums);
                    packages[key] = docs[0];
                    return docs[0];
                }
            };

            var perfTracker = new PerfEventTracker();
            PackageIndexing.UpdateIndex(WhatIf, adds, updates, deletes, packageFetcher, directory, Log, perfTracker, manager.Frameworks.Load());
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

        private static IDictionary<int, int> ParseRangeResult(string content)
        {
            IDictionary<int, int> result = new Dictionary<int, int>();
            JObject obj = JObject.Parse(content);
            foreach (KeyValuePair<string, JToken> property in obj)
            {
                result.Add(int.Parse(property.Key), property.Value.Value<int>());
            }
            return result;
        }
    }
}
