// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery
{
    public class GalleryCloudBlobContainerInformationProviderFacts
    {
        [Theory]
        [FolderNamesData()]
        public void ProvidesProperCacheControl(string folderName)
        {
            var target = new GalleryCloudBlobContainerInformationProvider();

            var cacheControl = target.GetCacheControl(folderName);

            if (folderName == CoreConstants.Folders.PackagesFolderName
                || folderName == CoreConstants.Folders.SymbolPackagesFolderName
                || folderName == CoreConstants.Folders.ValidationFolderName)
            {
                Assert.Equal(CoreConstants.DefaultCacheControl, cacheControl);
            }
            else
            {
                Assert.Null(cacheControl);
            }
        }

        [Theory]
        [FolderNamesData(includeContentTypes: true)]
        public void ProvidesProperContentType(string folderName, string expectedContentType)
        {
            var target = new GalleryCloudBlobContainerInformationProvider();

            var contentType = target.GetContentType(folderName);

            Assert.Equal(expectedContentType, contentType);
        }
    }
}
