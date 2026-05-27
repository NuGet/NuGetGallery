// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.AdminApi
{
    public class AdminApiTests : GalleryTestBase, IDisposable
    {
        private readonly HttpClient _httpClient;

        public AdminApiTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(UrlHelper.BaseUrl)
            };
        }

        // ================================================================
        // Auth tests (no valid token needed)
        // ================================================================

        public class TheAuthLayer : AdminApiTests
        {
            public TheAuthLayer(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns401WhenNoAuthorizationHeader(string endpoint)
            {
                var response = await PostJsonAsync(endpoint, "{}");

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                AssertHasWwwAuthenticateHeader(response);
            }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns401WhenWrongScheme(string endpoint)
            {
                var request = CreatePostRequest(endpoint, "{}");
                request.Headers.TryAddWithoutValidation("Authorization", "Basic dXNlcjpwYXNz");

                var response = await SendAsync(request);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns401WhenGarbageBearerToken(string endpoint)
            {
                var request = CreatePostRequest(endpoint, "{}");
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer not-a-jwt-at-all");

                var response = await SendAsync(request);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns403WhenWrongTenant(string endpoint)
            {
                var token = CreateFakeJwt("wrong-tenant-id", GetAllowedClientId());
                var request = CreatePostRequest(endpoint, "{}");
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

                var response = await SendAsync(request);

                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns403WhenWrongClientId(string endpoint)
            {
                var token = CreateFakeJwt(GetAllowedTenantId(), "wrong-client-id");
                var request = CreatePostRequest(endpoint, "{}");
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

                var response = await SendAsync(request);

                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }

        // ================================================================
        // Invalid JSON tests
        // ================================================================

        public class TheJsonErrorHandling : AdminApiTests
        {
            public TheJsonErrorHandling(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
            {
            }

            [Theory]
            [Priority(2)]
            [Category("AdminApiTests")]
            [InlineData("/api/admin/reflow-package")]
            [InlineData("/api/admin/lock-package")]
            [InlineData("/api/admin/lock-user")]
            [InlineData("/api/admin/soft-delete-package")]
            public async Task Returns400ForMalformedJson(string endpoint)
            {
                var response = await PostAuthenticatedJsonAsync(endpoint, "{not valid json");

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                await AssertJsonResponseAsync(response);
                await AssertResponseContainsAsync(response, "invalid JSON");
            }
        }

        // ================================================================
        // Happy-path tests (requires test mode + seeded data)
        // ================================================================

        public class TheEndpointOperations : AdminApiTests
        {
            public TheEndpointOperations(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
            {
            }

            [Fact]
            [Priority(2)]
            [Category("AdminApiTests")]
            public async Task ReflowsExistingPackage()
            {
                var config = GalleryConfiguration.Instance.AdminApi;
                var body = JsonConvert.SerializeObject(new
                {
                    packages = new[] { new { id = config.ReflowPackageId, version = config.ReflowPackageVersion } },
                    reason = "Functional test reflow"
                });

                var response = await PostAuthenticatedJsonAsync("/api/admin/reflow-package", body);

                var json = await ReadJsonAsync(response);
                TestOutputHelper.WriteLine($"Response: {json}");
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                var results = json["Results"] as JArray;
                Assert.NotNull(results);
                Assert.Single(results);
                Assert.Equal("Accepted", results[0]["Status"]?.ToString());
            }

            [Fact]
            [Priority(2)]
            [Category("AdminApiTests")]
            public async Task LocksExistingPackage()
            {
                var config = GalleryConfiguration.Instance.AdminApi;
                var body = JsonConvert.SerializeObject(new
                {
                    packages = new[] { new { id = config.LockPackageId } },
                    locked = true,
                    reason = "Functional test lock"
                });

                var response = await PostAuthenticatedJsonAsync("/api/admin/lock-package", body);

                var json = await ReadJsonAsync(response);
                TestOutputHelper.WriteLine($"Response: {json}");
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                var results = json["Results"] as JArray;
                Assert.NotNull(results);
                Assert.Single(results);
                Assert.Equal("Accepted", results[0]["Status"]?.ToString());
            }

            [Fact]
            [Priority(2)]
            [Category("AdminApiTests")]
            public async Task LocksExistingUser()
            {
                var config = GalleryConfiguration.Instance.AdminApi;
                var body = JsonConvert.SerializeObject(new
                {
                    users = new[] { new { username = config.LockUsername } },
                    locked = true
                });

                var response = await PostAuthenticatedJsonAsync("/api/admin/lock-user", body);

                var json = await ReadJsonAsync(response);
                TestOutputHelper.WriteLine($"Response: {json}");
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                var results = json["Results"] as JArray;
                Assert.NotNull(results);
                Assert.Single(results);
                Assert.Equal("Accepted", results[0]["Status"]?.ToString());
            }

            [Fact]
            [Priority(2)]
            [Category("AdminApiTests")]
            public async Task SoftDeletesExistingPackage()
            {
                var config = GalleryConfiguration.Instance.AdminApi;
                var body = JsonConvert.SerializeObject(new
                {
                    packages = new[] { new { id = config.SoftDeletePackageId, version = config.SoftDeletePackageVersion } },
                    reason = "Functional test soft-delete"
                });

                var response = await PostAuthenticatedJsonAsync("/api/admin/soft-delete-package", body);

                var json = await ReadJsonAsync(response);
                TestOutputHelper.WriteLine($"Response: {json}");
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                var results = json["Results"] as JArray;
                Assert.NotNull(results);
                Assert.Single(results);
                Assert.Equal("Accepted", results[0]["Status"]?.ToString());
            }

            [Fact]
            [Priority(2)]
            [Category("AdminApiTests")]
            public async Task WildcardSoftDeletesAllVersions()
            {
                // Uses a dedicated package ID (AdminApiTest.SoftDeleteAll) with two
                // seeded versions so this test doesn't conflict with SoftDeletesExistingPackage.
                var body = JsonConvert.SerializeObject(new
                {
                    packages = new[] { new { id = "AdminApiTest.SoftDeleteAll", version = "*" } },
                    reason = "Functional test wildcard soft-delete"
                });

                var response = await PostAuthenticatedJsonAsync("/api/admin/soft-delete-package", body);

                var json = await ReadJsonAsync(response);
                TestOutputHelper.WriteLine($"Response: {json}");

                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                var results = json["Results"] as JArray;
                Assert.NotNull(results);
                Assert.True(results.Count >= 2, "Expected at least two versions in the results.");
                Assert.True(
                    results.All(r => r["Status"]?.ToString() == "Accepted"),
                    "Expected all versions to be accepted for deletion.");
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static string GetAllowedTenantId()
        {
            return GalleryConfiguration.Instance.AdminApi?.AllowedTenantId ?? "your-tid";
        }

        private static string GetAllowedClientId()
        {
            return GalleryConfiguration.Instance.AdminApi?.AllowedClientId ?? "your-azp";
        }

        private static string CreateFakeJwt(string tid, string azp)
        {
            var header = JsonConvert.SerializeObject(new { alg = "RS256", typ = "JWT" });
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = JsonConvert.SerializeObject(new
            {
                iss = $"https://sts.windows.net/{tid}/",
                aud = "api://fake",
                iat = now,
                nbf = now,
                exp = now + 3600,
                tid,
                azp
            });

            var h = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
            var p = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            var s = Base64UrlEncode(Encoding.UTF8.GetBytes("fake-signature"));
            return $"{h}.{p}.{s}";
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string CreateAuthenticatedToken()
        {
            return CreateFakeJwt(GetAllowedTenantId(), GetAllowedClientId());
        }

        private async Task<HttpResponseMessage> PostJsonAsync(string endpoint, string body)
        {
            var request = CreatePostRequest(endpoint, body);
            return await SendAsync(request);
        }

        private async Task<HttpResponseMessage> PostAuthenticatedJsonAsync(string endpoint, string body)
        {
            var request = CreatePostRequest(endpoint, body);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {CreateAuthenticatedToken()}");
            return await SendAsync(request);
        }

        private static HttpRequestMessage CreatePostRequest(string endpoint, string body)
        {
            return new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            TestOutputHelper.WriteLine($"Request:  {request.Method} {request.RequestUri}");
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            TestOutputHelper.WriteLine($"Status:   {(int)response.StatusCode}");
            TestOutputHelper.WriteLine($"Response: {content}");
            return response;
        }

        private static async Task<JObject> ReadJsonAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }

        private static async Task AssertJsonResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("<html", content, StringComparison.OrdinalIgnoreCase);
            JObject.Parse(content); // Throws if not valid JSON
        }

        private static async Task AssertResponseContainsAsync(HttpResponseMessage response, string expected)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains(expected, content, StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertHasWwwAuthenticateHeader(HttpResponseMessage response)
        {
            Assert.True(
                response.Headers.WwwAuthenticate.Count > 0,
                "Expected WWW-Authenticate header in 401 response.");
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
