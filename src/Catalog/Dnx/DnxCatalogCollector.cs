// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxCatalogCollector : CommitCollector
    {
        DnxMaker _dnxMaker;

        public DnxCatalogCollector(Uri index, StorageFactory storageFactory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _dnxMaker = new DnxMaker(storageFactory);
        }

        public Uri ContentBaseAddress { get; set; }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            foreach (JToken item in items)
            {
                string id = item["nuget:id"].ToString().ToLowerInvariant();
                string version = item["nuget:version"].ToString().ToLowerInvariant();
                string type = item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet);

                if (type == Schema.DataTypes.PackageDetails.ToString())
                {
                    // Add/update package
                    string nuspec = await LoadNuspec(client, id, version, cancellationToken);
                    if (nuspec != null)
                    {
                        var requestUri = Utilities.GetNugetCacheBustingUri(new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version)));
                        using (Stream stream = await client.GetStreamAsync(requestUri))
                        {
                            await _dnxMaker.AddPackage(stream, nuspec, id, version, cancellationToken);
                        }

                        Trace.TraceInformation("commit: {0}/{1}", id, version);
                    }
                    else
                    {
                        Trace.TraceWarning("no nuspec available for {0}/{1} skipping", id, version);
                    }
                }
                else if (type == Schema.DataTypes.PackageDelete.ToString())
                {
                    await _dnxMaker.DeletePackage(id, version, cancellationToken);

                    Trace.TraceInformation("commit delete: {0}/{1}", id, version);
                }
            }

            return true;
        }

        private async Task<string> LoadNuspec(HttpClient client, string id, string version, CancellationToken cancellationToken)
        {
            var requestUri = Utilities.GetNugetCacheBustingUri(new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version)));
            HttpResponseMessage httpResponseMessage = await client.GetAsync(requestUri, cancellationToken);
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

        private static string GetNuspec(Stream stream, string id)
        {
            string name = string.Format("{0}.nuspec", id);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                // first look for a nuspec file named as the package id
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
                // failing that, just return the first file that appears to be a nuspec
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase))
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
    }
}
