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
    /// Covers staging management journeys in the browser without starting validation or promotion workers.
    /// </summary>
    [Collection(GalleryTestCollection.Definition)]
    public class StagingPageTests : NuGetPageTest
    {
        [Fact]
        [Category("PlaywrightTests")]
        [Category("StagingCiTests")]
        public async Task GroupListsParentAndSymbolsAndFiltersTheirRows()
        {
            await using var context = new StagingTestContext();
            var group = await context.CreateGroupAsync();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id), groupId: group))
            using (var symbols = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            await SignInAsStagingOwnerAsync();
            await OpenStagingPageAsync(StagingTestContext.ManagementUrl(id));
            var rows = Page.Locator(".staging-packages-table tbody tr");
            await Expect(rows).ToHaveCountAsync(2);
            await Expect(rows.First).ToContainTextAsync(id);
            await Expect(Page.Locator(".staging-parent-context")).ToContainTextAsync("Symbol package");
            await Expect(rows.First.Locator(".staging-status")).ToHaveTextAsync("Ready");
            await Expect(rows.Last.Locator(".staging-status")).ToHaveTextAsync("Ready");

            await Page.Locator("#staging-group-search").FillAsync("not-a-matching-package");
            await Expect(rows.First).ToBeHiddenAsync();
            await Expect(rows.Last).ToBeHiddenAsync();
            await Expect(Page.GetByText("No packages match your filter.", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Page.Locator("#staging-group-search").FillAsync(id);
            await Expect(rows.First).ToBeVisibleAsync();
            await Expect(rows.Last).ToBeVisibleAsync();
        }

        [Fact]
        [Category("PlaywrightTests")]
        [Category("StagingCiTests")]
        public async Task CreatesAndRenamesAGroupThroughItsForms()
        {
            await using var context = new StagingTestContext();
            var group = "staging-ui-" + Guid.NewGuid().ToString("N");
            context.TrackGroup(group);
            await SignInAsStagingOwnerAsync();
            await OpenStagingPageAsync(GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/account/staging/groups/new");
            await Page.Locator("input[name='Id']").FillAsync(group);
            await Page.Locator("input[name='Name']").FillAsync("Browser-created group");
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create group", Exact = true }).ClickAsync();
            await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath.Equals("/account/Packages", StringComparison.OrdinalIgnoreCase));
            var groupUrl = GalleryConfiguration.Instance.GalleryBaseUrl.TrimEnd('/') + "/account/staging/" + GalleryConfiguration.Instance.Account.Name + "/groups/" + group;
            await OpenStagingPageAsync(groupUrl);
            await Expect(Page.Locator("h1")).ToContainTextAsync("Browser-created group");
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Rename group", Exact = true }).ClickAsync();
            await Page.Locator("input[name='Name']").FillAsync("Renamed in browser");
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Confirm group name", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".staging-group-name")).ToHaveTextAsync("Renamed in browser");
            var detail = await context.GetJsonAsync("groups/" + group);
            Assert.Equal("Renamed in browser", detail["group"]["name"].GetValue<string>());
            Assert.Equal(group, detail["group"]["id"].GetValue<string>());
        }

        [Fact]
        [Category("PlaywrightTests")]
        [Category("StagingCiTests")]
        public async Task ListedIntentSurvivesReloadAndIsVisibleThroughTheApi()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var upload = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            {
                await StagingTestContext.ReadJsonAsync(upload, HttpStatusCode.Created);
            }

            await SignInAsStagingOwnerAsync();
            await OpenStagingPageAsync(StagingTestContext.ManagementUrl(id));
            var row = Page.Locator(".staging-packages-table tbody tr").Filter(new LocatorFilterOptions { HasText = id });
            var checkbox = row.Locator(".staging-listed-input");
            await Expect(checkbox).ToBeCheckedAsync();
            var response = await Page.RunAndWaitForResponseAsync(
                () => checkbox.UncheckAsync(),
                response => response.Url.EndsWith("/listed", StringComparison.Ordinal) && response.Request.Method == "POST");
            Assert.Equal(204, response.Status);
            var artifact = await context.GetJsonAsync(StagingTestContext.ArtifactPath(id) + "/status");
            Assert.False(artifact["listed"].GetValue<bool>());
            await Page.ReloadAsync();
            await Expect(checkbox).Not.ToBeCheckedAsync();
        }

        [Fact]
        [Category("PlaywrightTests")]
        [Category("StagingCiTests")]
        public async Task DeletingAParentShowsItsRetainedSymbolsWaitingForParent()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id)))
            using (var symbols = await context.UploadAsync(id, StagingTestContext.CreateArchive(id, symbols: true), symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            await SignInAsStagingOwnerAsync();
            await OpenStagingPageAsync(StagingTestContext.ManagementUrl(id));
            var parentRow = Page.Locator(".staging-packages-table tbody tr").Filter(new LocatorFilterOptions
            {
                HasText = id,
                Has = Page.Locator(".staging-listed-input"),
            });
            await parentRow.Locator(".staging-package-actions-toggle").ClickAsync();
            Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
            await parentRow.Locator(".staging-delete-trigger").ClickAsync();
            var symbolRow = Page.Locator(".staging-packages-table tbody tr").Filter(new LocatorFilterOptions { HasText = id });
            await Expect(symbolRow).ToHaveCountAsync(1);
            await Expect(symbolRow).ToContainTextAsync("Waiting for parent");
            var status = await context.GetJsonAsync(StagingTestContext.ArtifactPath(id, true) + "/status");
            Assert.Equal("waitingForParent", status["status"].GetValue<string>());
            using var missing = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id) + "/status");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        private async Task SignInAsStagingOwnerAsync()
        {
            await SignInAsync(GalleryConfiguration.Instance.Account.Name);
            await Expect(Page.Locator("span.dropdown-username")).ToContainTextAsync(GalleryConfiguration.Instance.Account.Name);
        }

        private async Task OpenStagingPageAsync(string url)
        {
            var response = await Page.GotoAsync(url);
            Assert.True(response.Status == 200, $"Staging page returned {response.Status}: {await response.TextAsync()}");
        }
    }
}
