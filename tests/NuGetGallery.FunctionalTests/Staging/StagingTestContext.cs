// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace NuGetGallery.FunctionalTests.Staging
{
    /// <summary>
    /// Creates private staging fixtures through HTTP and removes their active staging on disposal.
    /// </summary>
    internal sealed class StagingTestContext : IAsyncDisposable
    {
        private readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly HashSet<string> _packageIds = new HashSet<string>();
        private readonly HashSet<string> _groupIds = new HashSet<string>();
        private readonly HashSet<string> _publishedPackageIds = new HashSet<string>();

        internal StagingTestContext(string apiKey = null)
        {
            ApiKey = apiKey ?? GalleryConfiguration.Instance.Account.ApiKeyStage;
            Assert.False(string.IsNullOrWhiteSpace(ApiKey), "The functional-test configuration must provide a staging API key.");
            _client.BaseAddress = new Uri(GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/api/v3/staging/");
            _client.DefaultRequestHeaders.Add("X-NuGet-Client-Version", "6.0.0");
        }

        internal string ApiKey { get; }

        internal static string NewPackageId(string prefix = "StagingFunctional") => prefix + "." + Guid.NewGuid().ToString("N");

        internal static string ArtifactPath(string id, bool symbols = false) => (symbols ? "symbols/" : "package/") + id + "/1.0.0";

        internal static string ManagementUrl(string id) => GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/account/staging/package/" + id + "/1.0.0/manage";

        internal void TrackGroup(string id) => _groupIds.Add(id);

        internal void TrackPublication(string id) => _publishedPackageIds.Add(id);

        internal static byte[] CreateArchive(string id, bool symbols = false, string description = "Staging functional test", string assemblyName = null)
        {
            var metadata = new XElement("metadata",
                new XElement("id", id),
                new XElement("version", "1.0.0"),
                new XElement("description", description));
            if (symbols)
            {
                metadata.Add(new XElement("packageTypes", new XElement("packageType", new XAttribute("name", "SymbolsPackage"))));
            }
            else
            {
                metadata.Add(new XElement("authors", "NuGet"));
            }

            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var writer = new StreamWriter(archive.CreateEntry(id + ".nuspec").Open()))
                    {
                        new XDocument(new XElement("package", metadata)).Save(writer);
                    }

                    var extension = symbols ? ".pdb" : ".dll";
                    var sourcePath = Path.ChangeExtension(typeof(StagingTestContext).Assembly.Location, extension);
                    var archiveName = assemblyName ?? Path.GetFileNameWithoutExtension(sourcePath);
                    using (var source = File.OpenRead(sourcePath))
                    using (var target = archive.CreateEntry("lib/net10.0/" + archiveName + extension).Open())
                    {
                        source.CopyTo(target);
                    }
                }

                return stream.ToArray();
            }
        }

        internal async Task<HttpResponseMessage> UploadAsync(string id, byte[] bytes, bool symbols = false, string groupId = null, bool? listed = null)
        {
            _packageIds.Add(id);
            using (var content = new MultipartFormDataContent())
            {
                var field = symbols ? "symbols" : "package";
                content.Add(new ByteArrayContent(bytes), field, id + (symbols ? ".snupkg" : ".nupkg"));
                if (groupId != null)
                {
                    _groupIds.Add(groupId);
                    content.Add(new StringContent(groupId), "groupId");
                }

                if (listed.HasValue)
                {
                    content.Add(new StringContent(listed.Value.ToString()), "listed");
                }

                return await SendAsync(HttpMethod.Put, field, content);
            }
        }

        internal async Task<string> CreateGroupAsync()
        {
            var id = "staging-functional-" + Guid.NewGuid().ToString("N");
            _groupIds.Add(id);
            using (var content = new StringContent("{\"id\":\"" + id + "\",\"name\":\"Functional group\"}", Encoding.UTF8, "application/json"))
            using (var response = await SendAsync(HttpMethod.Post, "groups", content))
            {
                await ReadJsonAsync(response, HttpStatusCode.Created);
            }

            return id;
        }

        internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent content = null, string apiKey = null)
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                request.Content = content;
                if (method == HttpMethod.Put)
                {
                    request.Headers.TransferEncodingChunked = true;
                }

                var key = apiKey ?? ApiKey;
                if (!string.IsNullOrEmpty(key))
                {
                    request.Headers.Add("X-NuGet-ApiKey", key);
                }

                return await _client.SendAsync(request);
            }
        }

        internal async Task<JsonObject> GetJsonAsync(string path, string apiKey = null)
        {
            using (var response = await SendAsync(HttpMethod.Get, path, apiKey: apiKey))
            {
                return await ReadJsonAsync(response, HttpStatusCode.OK);
            }
        }

        internal Task<JsonObject> WaitForArtifactAsync(string id, bool symbols = false, string status = "ready")
        {
            return WaitForJsonAsync(ArtifactPath(id, symbols) + "/status", artifact =>
            {
                var actual = artifact["status"].GetValue<string>();
                if (actual != status && (actual == "validationFailed" || actual == "promotionFailed" || actual == "expired"))
                {
                    Assert.Fail($"Artifact reached an unexpected terminal state: {artifact}");
                }

                return actual == status;
            });
        }

        internal Task<JsonObject> WaitForGroupCompletionAsync(string groupId)
        {
            return WaitForJsonAsync("groups/" + groupId, detail =>
            {
                Assert.DoesNotContain(detail["items"].AsArray(), item => item["status"].GetValue<string>() == "promotionFailed");
                return detail["totalCount"].GetValue<int>() == 0
                    && detail["group"]["blockers"].AsArray().Any(blocker => blocker["code"].GetValue<string>() == "GroupEmpty");
            });
        }

        internal async Task<byte[]> DownloadPublishedAsync(string id, bool symbols = false)
        {
            var route = symbols ? "symbolpackage" : "package";
            using var redirect = await SendAsync(HttpMethod.Get, "../../../api/v2/" + route + "/" + id + "/1.0.0", apiKey: "");
            Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
            Assert.NotNull(redirect.Headers.Location);
            using var download = await SendAsync(HttpMethod.Get, redirect.Headers.Location.AbsoluteUri, apiKey: "");
            return await ReadBytesAsync(download);
        }

        private async Task<JsonObject> WaitForJsonAsync(string path, Func<JsonObject, bool> completed)
        {
            var timeout = TimeSpan.FromMinutes(4);
            var elapsed = Stopwatch.StartNew();
            JsonObject latest = null;
            while (elapsed.Elapsed < timeout)
            {
                latest = await GetJsonAsync(path);
                if (completed(latest))
                {
                    return latest;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            throw new TimeoutException($"Timed out after {timeout} waiting for {path}. Last response: {latest}");
        }

        internal static async Task<JsonObject> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expectedStatus, $"Expected {(int)expectedStatus}, got {(int)response.StatusCode}: {body}");
            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
            return Assert.IsType<JsonObject>(JsonNode.Parse(body));
        }

        internal static async Task<byte[]> ReadBytesAsync(HttpResponseMessage response)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Assert.Fail($"Download returned {(int)response.StatusCode}: {Encoding.UTF8.GetString(bytes)}");
            }

            return bytes;
        }

        internal static void AssertArtifact(JsonObject artifact, string id, bool symbols = false)
        {
            Assert.Equal(id, artifact["id"].GetValue<string>());
            Assert.Equal("1.0.0", artifact["version"].GetValue<string>());
            Assert.Equal(symbols ? "symbols" : "package", artifact["kind"].GetValue<string>());
            Assert.Equal("ready", artifact["status"].GetValue<string>());
            Assert.EndsWith("Z", artifact["uploaded"].GetValue<string>());
            Assert.EndsWith("Z", artifact["expires"].GetValue<string>());
            Assert.True(DateTimeOffset.Parse(artifact["expires"].GetValue<string>()) > DateTimeOffset.Parse(artifact["uploaded"].GetValue<string>()));
            Assert.False(artifact.ContainsKey("validated"));
            Assert.IsType<JsonArray>(artifact["blockers"]);
            Assert.True(Uri.TryCreate(artifact["managementUrl"].GetValue<string>(), UriKind.Absolute, out _));
            Assert.Equal(!symbols, artifact.ContainsKey("listed"));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                foreach (var id in _packageIds)
                {
                    await DeleteFixtureAsync(ArtifactPath(id, symbols: true));
                    await DeleteFixtureAsync(ArtifactPath(id));
                }

                foreach (var id in _groupIds)
                {
                    await DeleteFixtureAsync("groups/" + id);
                }

                foreach (var id in _publishedPackageIds)
                {
                    using var response = await SendAsync(HttpMethod.Delete, "../../../api/v2/package/" + id + "/1.0.0", apiKey: GalleryConfiguration.Instance.Account.ApiKeyUnlist);
                    Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
                        $"Unlisting the published fixture failed for {id}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                }
            }
            finally
            {
                _client.Dispose();
            }
        }

        private async Task DeleteFixtureAsync(string path)
        {
            using (var response = await SendAsync(HttpMethod.Delete, path))
            {
                Assert.True(response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound,
                    $"Fixture cleanup failed for {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
