// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Staging
{
    /// <summary>
    /// Exercises private staging HTTP contracts with the ci-gallery immediate-validation path.
    /// </summary>
    [Collection(GalleryTestCollection.Definition)]
    public class StagingApiTests
    {
        [Fact]
        [Category("StagingCiTests")]
        public async Task OwnerQuotaCountsBothKindsRejectsNewArtifactsAndAllowsReplacement()
        {
            await using var context = new StagingTestContext(GalleryConfiguration.Instance.StagingQuotaOrganization.ApiKeyStage);
            var firstId = StagingTestContext.NewPackageId();
            var secondId = StagingTestContext.NewPackageId();
            var rejectedId = StagingTestContext.NewPackageId();
            var empty = await context.GetJsonAsync("package");
            Assert.Equal(0, empty["quota"]["usedArtifacts"].GetValue<int>());
            Assert.Equal(3, empty["quota"]["limit"].GetValue<int>());
            foreach (var id in new[] { firstId, secondId })
            {
                using var upload = await context.UploadAsync(id, StagingTestContext.CreateArchive(id));
                await StagingTestContext.ReadJsonAsync(upload, HttpStatusCode.Created);
            }

            using (var symbols = await context.UploadAsync(firstId, StagingTestContext.CreateArchive(firstId, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            var full = await context.GetJsonAsync("package");
            Assert.Equal(full["quota"]["limit"].GetValue<int>(), full["quota"]["usedArtifacts"].GetValue<int>());
            foreach (var symbols in new[] { false, true })
            {
                var id = rejectedId;
                if (symbols)
                {
                    id = secondId;
                }

                using var rejected = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols), symbols);
                var error = await StagingTestContext.ReadJsonAsync(rejected, HttpStatusCode.Conflict);
                Assert.Contains("artifact limit", error["error"]["message"].GetValue<string>());
                Assert.Null(rejected.Headers.Location);
                using var absent = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id, symbols) + "/status");
                Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

                var replacement = StagingTestContext.CreateArchive(firstId, symbols, description: "Replacement at capacity");
                using var replaced = await context.UploadAsync(firstId, replacement, symbols);
                await StagingTestContext.ReadJsonAsync(replaced, HttpStatusCode.OK);
                using var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(firstId, symbols));
                Assert.Equal(replacement, await StagingTestContext.ReadBytesAsync(download));
            }

            Assert.Equal(3, (await context.GetJsonAsync("symbols"))["quota"]["usedArtifacts"].GetValue<int>());
            using (var delete = await context.SendAsync(HttpMethod.Delete, StagingTestContext.ArtifactPath(firstId, true)))
            {
                Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            }

            Assert.Equal(2, (await context.GetJsonAsync("package"))["quota"]["usedArtifacts"].GetValue<int>());
            using (var accepted = await context.UploadAsync(secondId, StagingTestContext.CreateArchive(secondId, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(accepted, HttpStatusCode.Created);
            }

            Assert.Equal(3, (await context.GetJsonAsync("symbols"))["quota"]["usedArtifacts"].GetValue<int>());
        }

        [Theory]
        [Category("StagingCiTests")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RestrictedOwnerCannotListDownloadOrDeletePrivateArtifacts(bool locked)
        {
            await using var context = new StagingTestContext(GalleryConfiguration.Instance.StagingRestrictedOrganization.ApiKeyStage);
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            using (var symbols = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            foreach (var route in new[] { "package", "symbols" })
            {
                var visible = await context.GetJsonAsync(route);
                Assert.Contains(visible["items"].AsArray(), item => item["id"].GetValue<string>() == id);
            }

            await using (var restriction = await StagingOwnerStateScope.RestrictAsync(locked))
            {
                foreach (var symbols in new[] { false, true })
                {
                    var inventory = await context.GetJsonAsync(symbols ? "symbols" : "package");
                    Assert.Empty(inventory["items"].AsArray());
                    Assert.Equal(0, inventory["totalCount"].GetValue<int>());
                    var path = StagingTestContext.ArtifactPath(id, symbols);
                    using var status = await context.SendAsync(HttpMethod.Get, path + "/status");
                    using var download = await context.SendAsync(HttpMethod.Get, path);
                    using var delete = await context.SendAsync(HttpMethod.Delete, path);
                    Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
                }
            }

            StagingTestContext.AssertArtifact(await context.GetJsonAsync(StagingTestContext.ArtifactPath(id) + "/status"), id);
            StagingTestContext.AssertArtifact(await context.GetJsonAsync(StagingTestContext.ArtifactPath(id, true) + "/status"), id, symbols: true);
        }

        [Theory]
        [Category("StagingCiTests")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task UploadStatusAndDownloadPreserveTheArtifact(bool symbols)
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
            }

            var bytes = StagingTestContext.CreateArchive(id, symbols);
            var expectedStatus = symbols ? HttpStatusCode.Created : HttpStatusCode.OK;
            using (var upload = await context.UploadAsync(id, bytes, symbols))
            {
                var artifact = await StagingTestContext.ReadJsonAsync(upload, expectedStatus);
                StagingTestContext.AssertArtifact(artifact, id, symbols);
                if (symbols)
                {
                    Assert.Equal(StagingTestContext.ArtifactPath(id, symbols) + "/status", upload.Headers.Location.AbsolutePath.Substring("/api/v3/staging/".Length));
                }
            }

            var path = StagingTestContext.ArtifactPath(id, symbols);
            StagingTestContext.AssertArtifact(await context.GetJsonAsync(path + "/status"), id, symbols);
            using (var download = await context.SendAsync(HttpMethod.Get, path))
            {
                Assert.Equal(bytes, await StagingTestContext.ReadBytesAsync(download));
                Assert.Contains(id, download.Content.Headers.ContentDisposition.FileName);
            }

            using (var publicPackage = await context.SendAsync(HttpMethod.Get, "../../../packages/" + id + "/1.0.0"))
            {
                Assert.Equal(HttpStatusCode.NotFound, publicPackage.StatusCode);
            }
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task ReplacementAndRestagingPreserveOnlyTheIntendedListedValue()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var initial = await context.UploadAsync(id, StagingTestContext.CreateArchive(id), listed: false))
            {
                var artifact = await StagingTestContext.ReadJsonAsync(initial, HttpStatusCode.Created);
                Assert.False(artifact["listed"].GetValue<bool>());
                Assert.EndsWith(StagingTestContext.ArtifactPath(id) + "/status", initial.Headers.Location.AbsolutePath);
            }

            var replacement = StagingTestContext.CreateArchive(id, description: "Replacement content");
            using (var replaced = await context.UploadAsync(id, replacement))
            {
                var artifact = await StagingTestContext.ReadJsonAsync(replaced, HttpStatusCode.OK);
                Assert.False(artifact["listed"].GetValue<bool>());
                Assert.Null(replaced.Headers.Location);
            }

            using (var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id)))
            {
                Assert.Equal(replacement, await StagingTestContext.ReadBytesAsync(download));
            }

            using (var deleted = await context.SendAsync(HttpMethod.Delete, StagingTestContext.ArtifactPath(id)))
            {
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
                Assert.Empty(await deleted.Content.ReadAsByteArrayAsync());
            }

            using (var restaged = await context.UploadAsync(id, replacement))
            {
                var artifact = await StagingTestContext.ReadJsonAsync(restaged, HttpStatusCode.OK);
                Assert.True(artifact["listed"].GetValue<bool>());
                Assert.Null(restaged.Headers.Location);
            }
        }

        [Theory]
        [Category("StagingCiTests")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InventoriesFilterBeforePagingAndReturnOwnerQuota(bool symbols)
        {
            await using var context = new StagingTestContext();
            var route = symbols ? "symbols" : "package";
            var before = await context.GetJsonAsync(route);
            var usedBefore = before["quota"]["usedArtifacts"].GetValue<int>();
            var prefix = "StagingFunctional.Allowed." + System.Guid.NewGuid().ToString("N");
            var firstId = prefix + ".First";
            var secondId = prefix + ".Second";
            var hiddenId = StagingTestContext.NewPackageId();
            foreach (var id in new[] { firstId, secondId, hiddenId })
            {
                using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
                {
                    await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                }

                if (symbols)
                {
                    using var upload = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true);
                    await StagingTestContext.ReadJsonAsync(upload, HttpStatusCode.Created);
                }
            }

            var key = GalleryConfiguration.Instance.Account.ApiKeyStagePattern;
            var first = await context.GetJsonAsync(route + "?page=1&pageSize=1", key);
            var second = await context.GetJsonAsync(route + "?page=2&pageSize=1", key);
            Assert.Equal(2, first["totalCount"].GetValue<int>());
            Assert.Equal(2, second["totalCount"].GetValue<int>());
            Assert.Equal(secondId, first["items"][0]["id"].GetValue<string>());
            Assert.Equal(firstId, second["items"][0]["id"].GetValue<string>());
            Assert.NotNull(first["quota"]);
            var addedArtifacts = 3;
            if (symbols)
            {
                addedArtifacts = 6;
            }

            Assert.Equal(usedBefore + addedArtifacts, first["quota"]["usedArtifacts"].GetValue<int>());
            Assert.True(first["quota"]["limit"].GetValue<int>() >= first["quota"]["usedArtifacts"].GetValue<int>());

            var beyondEnd = await context.GetJsonAsync(route + "?page=2147483647&pageSize=500", key);
            Assert.Empty(beyondEnd["items"].AsArray());
            Assert.Equal(2, beyondEnd["totalCount"].GetValue<int>());
            foreach (var suffix in new[] { "", "/status" })
            {
                using var invisible = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(hiddenId, symbols) + suffix, apiKey: key);
                Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
            }
        }

        [Theory]
        [Category("StagingCiTests")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OtherOwnerCannotReadOrDeletePrivateArtifacts(bool symbols)
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
            }

            if (symbols)
            {
                using var uploaded = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true);
                await StagingTestContext.ReadJsonAsync(uploaded, HttpStatusCode.Created);
            }

            var path = StagingTestContext.ArtifactPath(id, symbols);
            var otherKey = GalleryConfiguration.Instance.AdminOrganization.ApiKeyStage;
            using (var status = await context.SendAsync(HttpMethod.Get, path + "/status", apiKey: otherKey))
            using (var download = await context.SendAsync(HttpMethod.Get, path, apiKey: otherKey))
            using (var delete = await context.SendAsync(HttpMethod.Delete, path, apiKey: otherKey))
            {
                Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            }

            var inventory = await context.GetJsonAsync(symbols ? "symbols" : "package", otherKey);
            Assert.DoesNotContain(inventory["items"].AsArray(), item => item["id"].GetValue<string>() == id);
            StagingTestContext.AssertArtifact(await context.GetJsonAsync(path + "/status"), id, symbols);
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task OrganizationScopedCredentialStagesForTheOrganization()
        {
            var organization = GalleryConfiguration.Instance.AdminOrganization;
            await using var context = new StagingTestContext(organization.ApiKeyStage);
            var id = StagingTestContext.NewPackageId();
            using var uploaded = await context.UploadAsync(id, StagingTestContext.CreateArchive(id));
            var artifact = await StagingTestContext.ReadJsonAsync(uploaded, HttpStatusCode.Created);
            Assert.Equal(organization.Name, artifact["owner"].GetValue<string>());
            using var hidden = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id) + "/status", apiKey: GalleryConfiguration.Instance.Account.ApiKeyStage);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task GroupContractsIncludeBothKindsAndDeletionRemovesTheirStaging()
        {
            await using var context = new StagingTestContext();
            var group = await context.CreateGroupAsync();
            var id = StagingTestContext.NewPackageId();
            using (var package = await context.UploadAsync(id, StagingTestContext.CreateArchive(id), groupId: group))
            using (var symbols = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true))
            {
                var parent = await StagingTestContext.ReadJsonAsync(package, HttpStatusCode.Created);
                var symbol = await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
                Assert.Equal(group, parent["group"]["id"].GetValue<string>());
                Assert.Equal(group, symbol["group"]["id"].GetValue<string>());
                Assert.False(parent["canPromote"].GetValue<bool>());
            }

            var groups = await context.GetJsonAsync("groups?page=1&pageSize=500");
            Assert.Contains(groups["items"].AsArray(), item => item["id"].GetValue<string>() == group);
            var detail = await context.GetJsonAsync("groups/" + group);
            Assert.Equal(2, detail["totalCount"].GetValue<int>());
            Assert.Equal(2, detail["items"].AsArray().Count);
            using (var deleted = await context.SendAsync(HttpMethod.Delete, "groups/" + group))
            {
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }

            foreach (var path in new[] { "groups/" + group, StagingTestContext.ArtifactPath(id) + "/status", StagingTestContext.ArtifactPath(id, true) + "/status" })
            {
                using var missing = await context.SendAsync(HttpMethod.Get, path);
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            }
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task ParentDeletionRetainsSymbolsUntilTheParentIsRestaged()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            var packageBytes = StagingTestContext.CreateArchive(id);
            var symbolBytes = StagingTestContext.CreateArchive(id, symbols: true);
            using (var parent = await context.UploadAsync(id, packageBytes))
            using (var symbols = await context.UploadAsync(id, symbolBytes, symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            using (var delete = await context.SendAsync(HttpMethod.Delete, StagingTestContext.ArtifactPath(id)))
            {
                Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            }

            var symbolPath = StagingTestContext.ArtifactPath(id, true);
            var waiting = await context.GetJsonAsync(symbolPath + "/status");
            Assert.Equal("waitingForParent", waiting["status"].GetValue<string>());
            Assert.False(waiting["canPromote"].GetValue<bool>());
            using (var download = await context.SendAsync(HttpMethod.Get, symbolPath))
            {
                Assert.Equal(symbolBytes, await StagingTestContext.ReadBytesAsync(download));
            }

            using (var restaged = await context.UploadAsync(id, packageBytes))
            {
                await StagingTestContext.ReadJsonAsync(restaged, HttpStatusCode.OK);
            }

            StagingTestContext.AssertArtifact(await context.GetJsonAsync(symbolPath + "/status"), id, symbols: true);
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task SymbolDeletionDoesNotDeleteItsParent()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            using (var symbols = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            using (var delete = await context.SendAsync(HttpMethod.Delete, StagingTestContext.ArtifactPath(id, true)))
            {
                Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
                Assert.Empty(await delete.Content.ReadAsByteArrayAsync());
            }

            StagingTestContext.AssertArtifact(await context.GetJsonAsync(StagingTestContext.ArtifactPath(id) + "/status"), id);
            using var missing = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id, true) + "/status");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        [Theory]
        [Category("StagingCiTests")]
        [InlineData("", HttpStatusCode.Unauthorized)]
        [InlineData("invalid-api-key", HttpStatusCode.Forbidden)]
        [InlineData("push-only", HttpStatusCode.Forbidden)]
        public async Task SharedAuthenticationRejectsMissingInvalidOrNonStagingCredentials(string key, HttpStatusCode expectedStatus)
        {
            await using var context = new StagingTestContext();
            if (key == "push-only")
            {
                key = GalleryConfiguration.Instance.Account.ApiKeyPush;
            }

            using var response = await context.SendAsync(HttpMethod.Get, "package", apiKey: key);
            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [Fact]
        [Category("StagingCiTests")]
        public async Task InvalidPagingAndUnknownUploadFieldsReturnControllerErrors()
        {
            await using var context = new StagingTestContext();
            using (var invalidPage = await context.SendAsync(HttpMethod.Get, "symbols?page=0"))
            {
                var error = await StagingTestContext.ReadJsonAsync(invalidPage, HttpStatusCode.BadRequest);
                Assert.NotNull(error["error"]);
            }

            var id = StagingTestContext.NewPackageId();
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(StagingTestContext.CreateArchive(id)), "package", id + ".nupkg");
            content.Add(new StringContent("unexpected"), "extraField");
            using var invalidUpload = await context.SendAsync(HttpMethod.Put, "package", content);
            var uploadError = await StagingTestContext.ReadJsonAsync(invalidUpload, HttpStatusCode.BadRequest);
            Assert.NotNull(uploadError["error"]);
            using var missing = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id) + "/status");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }
}
