using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;

namespace Ng
{
    //BUG:  we really want to order-by the LastEdited (in the SortedDictionary) but include the Created in the data (as it is the published date)

    public static class Feed2Catalog
    {
        #region private_strings
        private const string createdDateProperty = "Created";
        private const string lastEditedDateProperty = "LastEdited";
        private const string publishedDateProperty = "Published";
        #endregion

        public struct PackageDates
        {
            public DateTime packageCreatedDate;
            public DateTime packageLastEditedDate;
            public DateTime packagePublishedDate;
        }

        static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created,LastEdited,Published",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=Created,LastEdited,Published",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        public static Task<SortedList<DateTime, IList<Tuple<Uri, PackageDates>>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeCreatedUri(source, since, top), "Created");
        }

        public static Task<SortedList<DateTime, IList<Tuple<Uri, PackageDates>>>> GetEditedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeLastEditedUri(source, since, top), "LastEdited");
        }

        private static DateTime ForceUTC(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }
            return date;
        }

        public static async Task<SortedList<DateTime, IList<Tuple<Uri, PackageDates>>>> GetPackages(HttpClient client, Uri uri, string keyDateProperty)
        {
            SortedList<DateTime, IList<Tuple<Uri, PackageDates>>> result = new SortedList<DateTime, IList<Tuple<Uri, PackageDates>>>();

            XElement feed;
            using (Stream stream = await client.GetStreamAsync(uri))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (XElement entry in feed.Elements(atom + "entry"))
            {
                Uri content = new Uri(entry.Element(atom + "content").Attribute("src").Value);
                string createdEntryValue = entry.Element(metadata + "properties").Element(dataservices + createdDateProperty).Value;
                DateTime createdDate = String.IsNullOrEmpty(createdEntryValue) ? DateTime.MinValue : DateTime.Parse(createdEntryValue);

                string lastEditedEntryValue = entry.Element(metadata + "properties").Element(dataservices + lastEditedDateProperty).Value;
                DateTime lastEditedDate = String.IsNullOrEmpty(lastEditedEntryValue) ? DateTime.MinValue : DateTime.Parse(lastEditedEntryValue);

                string publishedEntryValue = entry.Element(metadata + "properties").Element(dataservices + publishedDateProperty).Value;
                DateTime publishedDate = String.IsNullOrEmpty(publishedEntryValue) ? createdDate : DateTime.Parse(publishedEntryValue);

                string keyEntryValue = entry.Element(metadata + "properties").Element(dataservices + keyDateProperty).Value;
                DateTime keyDate = String.IsNullOrEmpty(keyEntryValue) ? createdDate : DateTime.Parse(keyEntryValue);

                // NOTE that DateTime returned by the v2 feed does not have Z at the end even though it is in UTC. So, the DateTime kind is unspecified
                // So, forcibly convert it to UTC here
                createdDate = ForceUTC(createdDate);
                lastEditedDate = ForceUTC(lastEditedDate);
                publishedDate = ForceUTC(publishedDate);
                keyDate = ForceUTC(keyDate);

                IList<Tuple<Uri, PackageDates>> contentUris;
                if (!result.TryGetValue(keyDate, out contentUris))
                {
                    contentUris = new List<Tuple<Uri, PackageDates>>();
                    result.Add(keyDate, contentUris);
                }

                PackageDates dates;
                dates.packageCreatedDate = createdDate;
                dates.packageLastEditedDate = lastEditedDate;
                dates.packagePublishedDate = publishedDate;
                contentUris.Add(new Tuple<Uri, PackageDates>(content, dates));
            }

            return result;
        }

        static async Task<DateTime> DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<Tuple<Uri, PackageDates>>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, bool createdPackages)
        {
            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            DateTime lastDate = createdPackages ? lastCreated : lastEdited;

            if (packages == null || packages.Count == 0)
            {
                return lastDate;
            }

            foreach (KeyValuePair<DateTime, IList<Tuple<Uri, PackageDates>>> entry in packages)
            {
                foreach (Tuple<Uri, PackageDates> packageItem in entry.Value)
                {
                    Uri uri = packageItem.Item1;
                    PackageDates pDates = packageItem.Item2;

                    HttpResponseMessage response = await client.GetAsync(uri);

                    if (response.IsSuccessStatusCode)
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            CatalogItem item = Utils.CreateCatalogItem(stream, entry.Key, null, uri.ToString(), pDates.packageCreatedDate, pDates.packageLastEditedDate, pDates.packagePublishedDate);

                            if (item != null)
                            {
                                writer.Add(item);

                                Trace.TraceInformation("Add: {0}", uri);
                            }
                            else
                            {
                                Trace.TraceWarning("Unable to extract metadata from: {0}", uri);
                            }
                        }
                    }
                    else
                    {
                        Trace.TraceWarning("Unable to download: {0}", uri);
                    }
                }

                lastDate = entry.Key;
            }

            lastCreated = createdPackages ? lastDate : lastCreated;
            lastEdited = !createdPackages ? lastDate : lastEdited;
            IGraph commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, lastEdited);

            await writer.Commit(commitMetadata);

            Trace.TraceInformation("COMMIT");

            return lastDate;
        }

        static async Task<DateTime?> GetCatalogProperty(Storage storage, string propertyName)
        {
            string json = await storage.LoadString(storage.ResolveUri("index.json"));

            if (json != null)
            {
                JObject obj = JObject.Parse(json);

                JToken token;
                if (obj.TryGetValue(propertyName, out token))
                {
                    return token.ToObject<DateTime>().ToUniversalTime();
                }
            }

            return null;
        }

        static async Task Loop(string gallery, StorageFactory storageFactory, bool verbose, int interval, DateTime? startDate)
        {
            Storage storage = storageFactory.Create();

            const string LastCreated = "nuget:lastCreated";
            const string LastEdited = "nuget:lastEdited";

            int top = 20;
            int timeout = 300;

            while (true)
            {
                Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

                HttpMessageHandler handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(timeout);

                    //  fetch and add all newly CREATED packages - in order
                    DateTime lastCreated = await GetCatalogProperty(storage, LastCreated) ?? (startDate ?? DateTime.MinValue.ToUniversalTime());
                    DateTime lastEdited = await GetCatalogProperty(storage, LastEdited) ?? lastCreated;

                    SortedList<DateTime, IList<Tuple<Uri, PackageDates>>> createdPackages;
                    DateTime previousLastCreated = DateTime.MinValue;
                    do
                    {
                        Trace.TraceInformation("CATALOG LastCreated: {0}", lastCreated.ToString("O"));

                        createdPackages = await GetCreatedPackages(client, gallery, lastCreated, top);
                        Trace.TraceInformation("FEED CreatedPackages: {0}", createdPackages.Count);

                        lastCreated = await DownloadMetadata2Catalog(client, createdPackages, storage, lastCreated, lastEdited, createdPackages: true);
                        if (previousLastCreated == lastCreated)
                        {
                            break;
                        }
                        previousLastCreated = lastCreated;
                    }
                    while (createdPackages.Count > 0);

                    //  THEN fetch and add all EDITED packages - in order

                    SortedList<DateTime, IList<Tuple<Uri, PackageDates>>> editedPackages;
                    DateTime previousLastEdited = DateTime.MinValue;
                    do
                    {
                        Trace.TraceInformation("CATALOG LastEdited: {0}", lastEdited.ToString("O"));

                        editedPackages = await GetEditedPackages(client, gallery, lastEdited, top);

                        Trace.TraceInformation("FEED EditedPackages: {0}", editedPackages.Count);

                        lastEdited = await DownloadMetadata2Catalog(client, editedPackages, storage, lastCreated, lastEdited, createdPackages: false);
                        if (previousLastEdited == lastEdited)
                        {
                            break;
                        }
                        previousLastEdited = lastEdited;
                    }
                    while (editedPackages.Count > 0);
                }

                Thread.Sleep(interval * 1000);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng feed2catalog -gallery <v2-feed-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-interval <seconds>] [-startDate <DateTime>]");
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null)
            {
                PrintUsage();
                return;
            }

            string gallery = CommandHelpers.GetGallery(arguments);
            if (gallery == null)
            {
                PrintUsage();
                return;
            }

            bool verbose = CommandHelpers.GetVerbose(arguments);

            int interval = CommandHelpers.GetInterval(arguments);

            DateTime startDate = CommandHelpers.GetStartDate(arguments);

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            if (storageFactory == null)
            {
                PrintUsage();
                return;
            }

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\" interval: {2}", gallery, storageFactory, interval);
            DateTime? nullableStartDate = null;
            if (startDate != DateTime.MinValue)
            {
                nullableStartDate = startDate;
            }
            Loop(gallery, storageFactory, verbose, interval, nullableStartDate).Wait();
        }
    }
}
