using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class Feed2CatalogTests
    {
        public static void Test0(string[] args)
        {
            const string V2FeedCountQuery = "/Packages/$count";
            Console.WriteLine("Simple count test for distinct package ids and version between v2 feed and catalog");

            if(args.Length != 2)
            {
                Console.WriteLine("Please enter only 2 arguments. First v2gallery feed url, and second catalog index.json url");
                return;
            }
            else
            {
                string v2galleryfeedurl = args[0].TrimEnd('/');
                string catalog = args[1];
                Uri catalogIndex = new Uri(catalog);

                CatalogIndexReader reader = new CatalogIndexReader(catalogIndex);

                var task = reader.GetEntries();
                task.Wait();

                var entries = task.Result;
                Console.WriteLine("Total packages count from catalog is " + entries.Count());
                var distinctCatalogPackages = entries.Distinct(new CatalogIndexEntryPackageComparer());
                Console.WriteLine("Distinct packages count from catalog is " + distinctCatalogPackages.Count());

                Console.WriteLine("Distinct packages count from " + v2galleryfeedurl + ", can be obtained at " + v2galleryfeedurl + V2FeedCountQuery);
            }
        }
    }
}
