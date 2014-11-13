using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class Feed2CatalogTests
    {
        public static void Test0(string[] args)
        {
            Test0Async(args).Wait();
        }

        public static async Task Test0Async(string[] args)
        {
            const string V2FeedCountQuery = "/Packages/$count";
            Console.WriteLine("Simple count test for distinct package ids and version between v2 feed and catalog");

            if (args.Length != 2)
            {
                Console.WriteLine("Please enter only 2 arguments. First v2gallery feed url, and second catalog index.json url");
                return;
            }
            else
            {
                string v2FeedUrl = args[0].TrimEnd('/');
                string v2FeedCountUrl = v2FeedUrl.TrimEnd('/') + V2FeedCountQuery;
                int v2FeedCount = 0;
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(v2FeedCountUrl);
                    string v2FeedCountString = await response.Content.ReadAsStringAsync();
                    v2FeedCount = Int32.Parse(v2FeedCountString);
                }

                string catalog = args[1];
                Uri catalogIndex = new Uri(catalog);

                CatalogIndexReader reader = new CatalogIndexReader(catalogIndex);

                var task = reader.GetEntries();
                task.Wait();

                var entries = task.Result;
                Console.WriteLine("Total packages count from catalog is " + entries.Count());
                var distinctCatalogPackages = entries.Distinct(new CatalogIndexEntryIdVersionComparer());
                int v3CatalogPackagesCount = distinctCatalogPackages.Count();
                Console.WriteLine("Distinct packages count from catalog is " + v3CatalogPackagesCount);
                Console.WriteLine("Distinct packages count from " + v2FeedUrl + " is " + v2FeedCount);

                Console.WriteLine("Current difference between v2Feed and v3 catalog is " + (v2FeedCount - v3CatalogPackagesCount));
            }
        }
    }
}
