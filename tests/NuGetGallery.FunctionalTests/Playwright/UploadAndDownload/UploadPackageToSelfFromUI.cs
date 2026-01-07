// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.XunitExtensions;

namespace NuGetGallery.FunctionalTests.Playwright.UploadAndDownload
{
	public class UploadPackageToSelfFromUI : NuGetPageTest
	{
		[PackageLockFact]
		[Priority(0)]
		[Category("P0Tests")]
		public async Task UploadNewPackageRegistrationAsSelf()
		{
			// Arrange
			var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.Account.Name;

            // Act
            await SignInAsync();
			await UploadPackageAsync(packageId, "1.0.0", owner);
			// Assert
			await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
			await Expect(Page.Locator(".package-title")).ToContainTextAsync("1.0.0");
		}

		[PackageLockFact]
		[Priority(0)]
		[Category("P0Tests")]
		public async Task UploadNewVersionOfExistingPackageAsSelf()
		{
			// Arrange
			var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.Account.Name;

            // Act - Upload first version
            await SignInAsync();
			await UploadPackageAsync(packageId, "1.0.0", owner);

			// Act - Upload second version
			await UploadPackageAsync(packageId, "2.0.0", owner: null);

			// Assert
			await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
			await Expect(Page.Locator(".package-title")).ToContainTextAsync("2.0.0");
		}

        [PackageLockFact]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadNewPackageRegistrationAsCollaborator()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.CollaboratorOrganization.Name;

            // Act
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", owner);

            // Assert
            await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
            await Expect(Page.Locator(".package-title")).ToContainTextAsync("1.0.0");
        }

        [PackageLockFact]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadNewVersionOfExistingPackageAsCollaborator()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.CollaboratorOrganization.Name;

            // Act - Upload first version
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", owner);

            // Act - Upload second version
            await UploadPackageAsync(packageId, "2.0.0", owner: null);

            // Assert
            await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
            await Expect(Page.Locator(".package-title")).ToContainTextAsync("2.0.0");
        }

        [PackageLockFact]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadNewPackageRegistrationAsAdmin()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.AdminOrganization.Name;

            // Act
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", owner);

            // Assert
            await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
            await Expect(Page.Locator(".package-title")).ToContainTextAsync("1.0.0");
        }

        [PackageLockFact]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadNewVersionOfExistingPackageAsAdmin()
        {
            // Arrange
            var packageId = UploadHelper.GetUniquePackageId();
            var owner = GalleryConfiguration.Instance.AdminOrganization.Name;

            // Act - Upload first version
            await SignInAsync();
            await UploadPackageAsync(packageId, "1.0.0", owner);

            // Act - Upload second version
            await UploadPackageAsync(packageId, "2.0.0", owner: null);

            // Assert
            await Expect(Page.Locator("h1")).ToContainTextAsync(packageId);
            await Expect(Page.Locator(".package-title")).ToContainTextAsync("2.0.0");
        }
    }
}
