// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NuGetGallery.FunctionalTests.Staging;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.Staging
{
    /// <summary>
    /// Publishes a validated group through the UI with real promotion workers and development symbol ingestion.
    /// </summary>
    [Collection(GalleryTestCollection.Definition)]
    public class StagingPromotionPageTests : NuGetPageTest
    {
        [Fact]
        [Category("PlaywrightTests")]
        [Category("StagingFullTests")]
        public async Task GroupPromotionPublishesParentAndSymbolsPreservesListedIntentAndClearsStaging()
        {
            Assert.Equal("full", Environment.GetEnvironmentVariable("APPHOST_PROFILE"));
            await using var context = new StagingTestContext();
            var usedBefore = (await context.GetJsonAsync("package"))["quota"]["usedArtifacts"].GetValue<int>();
            var group = await context.CreateGroupAsync();
            var unlistedId = StagingTestContext.NewPackageId();
            var listedId = StagingTestContext.NewPackageId();
            var unlistedBytes = StagingTestContext.CreateArchive(unlistedId);
            var listedBytes = StagingTestContext.CreateArchive(listedId);
            var symbolBytes = StagingTestContext.CreateArchive(unlistedId, symbols: true);
            using (var unlisted = await context.UploadAsync(unlistedId, unlistedBytes, groupId: group, listed: false))
            using (var listed = await context.UploadAsync(listedId, listedBytes, groupId: group, listed: true))
            using (var symbols = await context.UploadAsync(unlistedId, symbolBytes, symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(unlisted, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(listed, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            await Task.WhenAll(context.WaitForArtifactAsync(unlistedId), context.WaitForArtifactAsync(listedId), context.WaitForArtifactAsync(unlistedId, symbols: true));
            Assert.True((await context.GetJsonAsync("groups/" + group))["group"]["canPromote"].GetValue<bool>());
            await SignInAsync(GalleryConfiguration.Instance.Account.Name);
            await Expect(Page.Locator("span.dropdown-username")).ToContainTextAsync(GalleryConfiguration.Instance.Account.Name);
            var url = GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/account/staging/" + GalleryConfiguration.Instance.Account.Name + "/groups/" + group;
            var pageResponse = await Page.GotoAsync(url);
            Assert.Equal(200, pageResponse.Status);
            await Expect(Page.Locator(".staging-packages-table tbody tr")).ToHaveCountAsync(3);
            var promote = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Promote group", Exact = true });
            await Expect(promote).ToBeEnabledAsync();
            Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
            var accepted = await Page.RunAndWaitForResponseAsync(
                () => promote.ClickAsync(),
                response => response.Url.EndsWith("/promote", StringComparison.Ordinal) && response.Request.Method == "POST");
            Assert.Equal(302, accepted.Status);
            await context.WaitForGroupCompletionAsync(group);
            context.TrackPublication(unlistedId);
            context.TrackPublication(listedId);

            foreach (var id in new[] { unlistedId, listedId })
            {
                var expected = listedBytes;
                if (id == unlistedId)
                {
                    expected = unlistedBytes;
                }

                Assert.Equal(expected, await context.DownloadPublishedAsync(id));
                using var missing = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id) + "/status");
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            }

            Assert.Equal(symbolBytes, await context.DownloadPublishedAsync(unlistedId, symbols: true));

            using (var missing = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(unlistedId, true) + "/status"))
            {
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            }

            Assert.Equal(usedBefore, (await context.GetJsonAsync("package"))["quota"]["usedArtifacts"].GetValue<int>());
            await Page.ReloadAsync();
            await Expect(Page.GetByText("This staging group has no packages.", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            var unlistedPage = await Page.GotoAsync(GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/packages/" + unlistedId + "/1.0.0");
            Assert.Equal(200, unlistedPage.Status);
            await Expect(Page.GetByText("This package is unlisted and hidden from package listings.", new PageGetByTextOptions { Exact = false })).ToBeVisibleAsync();
            var listedPage = await Page.GotoAsync(GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/packages/" + listedId + "/1.0.0");
            Assert.Equal(200, listedPage.Status);
            await Expect(Page.GetByText("This package is unlisted and hidden from package listings.", new PageGetByTextOptions { Exact = false })).ToHaveCountAsync(0);
        }
    }
}
