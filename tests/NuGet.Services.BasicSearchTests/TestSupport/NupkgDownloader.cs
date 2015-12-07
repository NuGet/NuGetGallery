// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public interface INupkgDownloader
    {
        Task DownloadPackagesAsync(IEnumerable<PackageVersion> packages);

        string GetPackagePath(PackageVersion version);
    }

    public class NupkgDownloader : INupkgDownloader
    {
        private const string PackageBaseAddressType = "PackageBaseAddress/3.0.0";
        private static readonly object Lock = new object();
        private static readonly IDictionary<string, SemaphoreSlim> PackageLocks = new Dictionary<string, SemaphoreSlim>();

        private readonly TestSettings _settings;
        private readonly HttpClient _client;
        private string _packageBaseAddress;

        public NupkgDownloader(TestSettings settings)
        {
            _settings = settings;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "NuGet Test Client");
        }

        public async Task DownloadPackagesAsync(IEnumerable<PackageVersion> packages)
        {
            Directory.CreateDirectory(_settings.PackageDirectory);
            await Task.WhenAll(packages.Select(DownloadPackageAsync));
        }

        public string GetPackagePath(PackageVersion version)
        {
            return Path.Combine(_settings.PackageDirectory, $"{version.Id}.{version.Version}.nupkg".ToLower());
        }

        private async Task DownloadPackageAsync(PackageVersion version)
        {
            var path = GetPackagePath(version);
            if (File.Exists(path))
            {
                return;
            }

            // download the package
            if (_packageBaseAddress == null)
            {
                _packageBaseAddress = await GetPackageBaseAddressAsync();
            }

            string relativeUri = $"{version.Id}/{version.Version}/{version.Id}.{version.Version}.nupkg".ToLower();
            var requestUri = new Uri(new Uri(_packageBaseAddress, UriKind.Absolute), relativeUri);
            var response = await _client.GetAsync(requestUri);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} was encountered when fetching package '{version.Id}' (version '{version.Version}').");
            }

            var packageStream = await response.Content.ReadAsStreamAsync();

            // get or initialize the package lock
            SemaphoreSlim packageLock;
            lock (Lock)
            {
                if (!PackageLocks.TryGetValue(path, out packageLock))
                {
                    packageLock = new SemaphoreSlim(1);
                    PackageLocks[path] = packageLock;
                }
            }

            // get a lock and write the package to disk
            var acquired = await packageLock.WaitAsync(TimeSpan.FromSeconds(30));
            if (!acquired)
            {
                throw new InvalidOperationException($"Could not get a lock to write the package '{version.Id}' (version '{version.Version}') to '{path}'.");
            }

            try
            {
                if (!File.Exists(path))
                {
                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        await packageStream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (IOException)
            {
                // an IOException is thrown when another thread already downloaded this package
            }
            finally
            {
                packageLock.Release();
            }
        }

        private async Task<string> GetPackageBaseAddressAsync()
        {
            var serializer = new JsonSerializer();
            JObject apiIndex;
            using (var stream = await _client.GetStreamAsync(_settings.ApiIndexUrl))
            using (var streamReader = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                apiIndex = serializer.Deserialize<JObject>(jsonTextReader);
            }

            var resource = apiIndex["resources"]
                .AsJEnumerable()
                .Select(t => t.ToObject<JObject>())
                .FirstOrDefault(r => r["@type"].Value<string>() == PackageBaseAddressType);

            if (resource == null)
            {
                throw new InvalidOperationException($"The resource with @type '{PackageBaseAddressType}' could not be found in '{_settings.ApiIndexUrl}'.");
            }

            return resource["@id"].Value<string>();
        }
    }
}