// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Playwright;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.Sponsorship
{
    [Collection(GalleryTestCollection.Definition)]
    public class SponsorshipTest : NuGetPageTest
    {
        private const string SupportedPlatformError = "Please provide a valid URL from a supported sponsorship platform.";

        [Fact]
        [Priority(1)]
        [Category("PlaywrightTests")]
        public async Task OwnerCanAddDisplayAndRemoveSponsorshipLink()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var sponsorshipUrl = $"https://github.com/sponsors/{packageId.ToLowerInvariant()}";
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", GalleryConfiguration.Instance.Account.Name);

            // Act
            await OpenSponsorshipSectionAsync(packageId);
            await AddSponsorshipLinkAsync(packageId, sponsorshipUrl);

            // Assert
            var section = Page.Locator(".page-manage-sponsorship-links");
            await Expect(section.GetByText("Sponsorship URL added successfully.")).ToBeVisibleAsync();
            var managedLink = section.GetByRole(AriaRole.Link, new() { Name = sponsorshipUrl });
            await Expect(managedLink).ToHaveAttributeAsync("href", sponsorshipUrl);
            await Expect(section.GetByRole(
                AriaRole.Button,
                new() { Name = $"Remove sponsorship link {sponsorshipUrl}" })).ToBeVisibleAsync();

            // Act
            await Page.GotoAsync(UrlHelper.GetPackagePageUrl(packageId));
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sponsor" }).ClickAsync();

            // Assert
            var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "External Links" });
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(dialog.GetByText(
                "These links will take you to third-party platforms. Microsoft is not affiliated with or responsible for the content or practices of third-party platforms."))
                .ToBeVisibleAsync();
            var publicLink = dialog.GetByRole(AriaRole.Link, new() { Name = sponsorshipUrl });
            await Expect(publicLink).ToHaveAttributeAsync("href", sponsorshipUrl);
            await Expect(publicLink).ToHaveAttributeAsync("target", "_blank");
            await Expect(publicLink).ToHaveAttributeAsync("rel", "nofollow noopener noreferrer");

            // Act
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close sponsorship popup" }).ClickAsync();
            await OpenSponsorshipSectionAsync(packageId);
            Page.Dialog += async (_, confirmation) => await confirmation.AcceptAsync();
            await section.GetByRole(
                AriaRole.Button,
                new() { Name = $"Remove sponsorship link {sponsorshipUrl}" }).ClickAsync();

            // Assert
            await Expect(section.GetByText("Sponsorship URL removed successfully.")).ToBeVisibleAsync();
            await Expect(section.GetByText("No sponsorship links have been added.")).ToBeVisibleAsync();
            await Page.GotoAsync(UrlHelper.GetPackagePageUrl(packageId));
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sponsor" })).ToHaveCountAsync(0);
        }

        [Fact]
        [Priority(1)]
        [Category("PlaywrightTests")]
        public async Task InvalidUnsupportedAndDuplicateSponsorshipLinksAreRejected()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var sponsorshipUrl = $"https://github.com/sponsors/{packageId.ToLowerInvariant()}";
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", GalleryConfiguration.Instance.Account.Name);
            await OpenSponsorshipSectionAsync(packageId);
            var section = Page.Locator(".page-manage-sponsorship-links");
            var urlInput = section.GetByLabel("URL");
            var addButton = section.GetByRole(
                AriaRole.Button,
                new() { Name = $"Add sponsorship link to {packageId}" });

            // Act
            await addButton.ClickAsync();

            // Assert
            await Expect(section.GetByText("Please enter a URL.")).ToBeVisibleAsync();

            // Act
            await urlInput.FillAsync("not-a-url");
            await addButton.ClickAsync();

            // Assert
            await Expect(section.GetByText(SupportedPlatformError)).ToBeVisibleAsync();
            await Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0);

            // Act
            await urlInput.FillAsync("https://example.com/sponsor");
            await addButton.ClickAsync();

            // Assert
            await Expect(section.GetByText(SupportedPlatformError)).ToBeVisibleAsync();
            await Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0);

            // Act
            await AddSponsorshipLinkAsync(packageId, sponsorshipUrl);
            await Expect(section.GetByText("Sponsorship URL added successfully.")).ToBeVisibleAsync();
            await urlInput.FillAsync(sponsorshipUrl);
            await addButton.ClickAsync();

            // Assert
            await Expect(section.GetByText("Sponsorship URL already exists.")).ToBeVisibleAsync();
            await Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        }

        [Fact]
        [Priority(1)]
        [Category("PlaywrightTests")]
        public async Task CollaboratorCannotAddSponsorshipLink()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var sponsorshipUrl = $"https://github.com/sponsors/{packageId.ToLowerInvariant()}";
            await SignInAsync();
            await UploadPackageAsync(
                packageId,
                "1.0.0",
                GalleryConfiguration.Instance.CollaboratorOrganization.Name);

            // Act
            await OpenSponsorshipSectionAsync(packageId);
            await AddSponsorshipLinkAsync(packageId, sponsorshipUrl);

            // Assert
            var section = Page.Locator(".page-manage-sponsorship-links");
            await Expect(section.GetByText(
                "You do not have permission to manage sponsorship links for this package."))
                .ToBeVisibleAsync();
            await Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0);
            await Page.GotoAsync(UrlHelper.GetPackagePageUrl(packageId));
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sponsor" })).ToHaveCountAsync(0);
        }

        private async Task OpenSponsorshipSectionAsync(string packageId)
        {
            await Page.GotoAsync(UrlHelper.GetPackageManagePageUrl(packageId));
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sponsorship Links" }).ClickAsync();
            await Expect(Page.Locator("#SponsorshipLinks-container")).ToBeVisibleAsync();
        }

        private async Task AddSponsorshipLinkAsync(string packageId, string sponsorshipUrl)
        {
            var section = Page.Locator(".page-manage-sponsorship-links");
            await section.GetByLabel("URL").FillAsync(sponsorshipUrl);
            await section.GetByRole(
                AriaRole.Button,
                new() { Name = $"Add sponsorship link to {packageId}" }).ClickAsync();
        }
    }
}
