// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
    class PartitioningTests
    {
        static void PrintMetrics(IDictionary<string, IList<JObject>> packages)
        {
            Console.WriteLine();
            Console.WriteLine(packages.Count);
            int count = 0;
            foreach (var versions in packages.Values)
            {
                count += versions.Count;
            }
            Console.WriteLine(count);
        }

        static async Task<HashSet<string>> GetPopularPackages()
        {
            Uri reportUri = new Uri("https://nugetgallery.blob.core.windows.net/stats/popularity/recentpopularity.json");

            HttpClient client = new HttpClient();

            Console.WriteLine(reportUri);
            HttpResponseMessage reportResponse = await client.GetAsync(reportUri);
            string reportJson = await reportResponse.Content.ReadAsStringAsync();
            JToken report = JToken.Parse(reportJson);

            HashSet<string> packages = new HashSet<string>();

            foreach (JObject item in report)
            {
                string packageId = item["PackageId"].ToString().ToLowerInvariant();
                packages.Add(packageId);
            }

            return packages;
        }

        public static void Test0()
        {
            Uri indexUri = new Uri("http://nugetprod0.blob.core.windows.net/ng-catalogs/0/index.json");

            IDictionary<string, IList<JObject>> packages = IndexingHelpers.GetPackages(indexUri, true, CancellationToken.None).Result;

            PrintMetrics(packages);

            int partitionCount = 100;

            int count = packages.Count;
            int chunkSize = count / (partitionCount - 1);

            IDictionary<string, IList<JObject>>[] partitions = new Dictionary<string, IList<JObject>>[partitionCount];

            IDictionary<string, IList<JObject>> batch = new Dictionary<string, IList<JObject>>();

            int batchNumber = 0;

            foreach (KeyValuePair<string, IList<JObject>> package in packages)
            {
                batch.Add(package);

                if (batch.Count == chunkSize)
                {
                    partitions[batchNumber++] = new Dictionary<string, IList<JObject>>(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                partitions[batchNumber++] = new Dictionary<string, IList<JObject>>(batch);
            }

            //CloudStorageAccount account = new CloudStorageAccount(new StorageCredentials("partitions", ""), false);

            IList<Task> createTasks = new List<Task>();

            int partitionNumber = 0;
            foreach (IDictionary<string, IList<JObject>> partition in partitions)
            {
                string name = string.Format("partition{0}", partitionNumber++);
                Storage storage = new FileStorage("http://localhost:8000/partition/" + name, @"c:\data\site\partition\" + name);
                //Storage storage = new AzureStorage(account, name);
                createTasks.Add(IndexingHelpers.CreateNewCatalog(storage, partition, CancellationToken.None));
            }

            Task.WaitAll(createTasks.ToArray());
        }

        public static void Test1()
        {
            string template = "http://partitions.blob.core.windows.net/partition{0}/index.json";

            IList<Task<IDictionary<string, IList<JObject>>>> tasks = new List<Task<IDictionary<string, IList<JObject>>>>();

            for (int i = 0; i < 100; i++)
            {
                Uri indexUri = new Uri(string.Format(template, i));

                tasks.Add(IndexingHelpers.GetPackages(indexUri, true, CancellationToken.None));
            }

            Task.WaitAll(tasks.ToArray());

            int packageCount = 0;
            int packageVersionCount = 0;

            foreach (Task<IDictionary<string, IList<JObject>>> task in tasks)
            {
                IDictionary<string, IList<JObject>> result = task.Result;

                packageCount += result.Count;

                foreach (KeyValuePair<string, IList<JObject>> package in result)
                {
                    packageVersionCount += package.Value.Count;
                }
            }

            Console.WriteLine(packageCount);
            Console.WriteLine(packageVersionCount);
        }
    }
}
