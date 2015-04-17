using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    public class DnxCatalogCollector : CommitCollector
    {
        StorageFactory _storageFactory;

        public DnxCatalogCollector(Uri index, StorageFactory storageFactory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _storageFactory = storageFactory;
        }

        public Uri ContentBaseAddress { get; set; }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            foreach (JToken item in items)
            {
                string id = item["nuget:id"].ToString().ToLowerInvariant();
                string version = item["nuget:version"].ToString().ToLowerInvariant();

                Storage storage = _storageFactory.Create(id);
                string nuspec = await LoadNuspec(id, version);

                if (nuspec != null)
                {
                    await SaveNuspec(storage, id, version, nuspec);
                    await CopyNupkg(storage, id, version);
                    await UpdateMetadata(storage, version);

                    Trace.TraceInformation("commit: {0}/{1}", id, version);
                }
                else
                {
                    Trace.TraceInformation("no nuspec available for {0}/{1} skipping", id, version);
                }
            }

            return true;
        }

        async Task UpdateMetadata(Storage storage, string version)
        {
            Uri resourceUri = new Uri(storage.BaseAddress, "index.json");
            HashSet<NuGetVersion> versions = GetVersions(await storage.LoadString(resourceUri));
            versions.Add(NuGetVersion.Parse(version));
            List<NuGetVersion> result = new List<NuGetVersion>(versions);
            result.Sort();
            await storage.Save(resourceUri, CreateContent(result.Select((v) => v.ToString())));
        }

        async Task<string> LoadNuspec(string id, string version)
        {
            using (HttpClient client = new HttpClient())
            {
                Uri requestUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));
                HttpResponseMessage httpResponseMessage = await client.GetAsync(requestUri);
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                    {
                        string nuspec = GetNuspec(stream, id);
                        return nuspec;
                    }
                }
                else if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Trace.TraceInformation("package not found...");
                }
                else
                {
                    httpResponseMessage.EnsureSuccessStatusCode();
                }
                return null;
            }
        }

        async Task SaveNuspec(Storage storage, string id, string version, string nuspec)
        {
            string relativeAddress = string.Format("{1}/{0}.nuspec", id, version);
            Uri nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            await storage.Save(nuspecUri, new StringStorageContent(nuspec, "text/xml"));
        }

        static HashSet<NuGetVersion> GetVersions(string json)
        {
            HashSet<NuGetVersion> result = new HashSet<NuGetVersion>();
            if (json != null)
            {
                JObject obj = JObject.Parse(json);
                foreach (JToken version in obj["versions"])
                {
                    result.Add(NuGetVersion.Parse(version.ToString()));
                }
            }
            return result;
        }

        StorageContent CreateContent(IEnumerable<string> versions)
        {
            JObject obj = new JObject { { "versions", new JArray(versions) } };
            return new StringStorageContent(obj.ToString(), "application/json");
        }

        static string GetNuspec(Stream stream, string id)
        {
            string name = string.Format("{0}.nuspec", id);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            return null;
        }

        async Task CopyNupkg(Storage storage, string id, string version)
        {
            using (HttpClient client = new HttpClient())
            {
                Uri requestUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));
                using (Stream stream = await client.GetStreamAsync(requestUri))
                {
                    string relativeAddress = string.Format("{1}/{0}.{1}.nupkg", id, version);
                    Uri nupkgUri = new Uri(storage.BaseAddress, string.Format("{1}/{0}.{1}.nupkg", id, version));
                    await storage.Save(nupkgUri, new StreamStorageContent(stream, "application/octet-stream"));
                }
            }
        }
    }
}
