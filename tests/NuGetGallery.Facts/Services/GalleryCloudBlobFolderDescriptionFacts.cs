// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NuGetGallery.Services
{
    public class GalleryCloudBlobFolderDescriptionFacts
    {
        public static IEnumerable<object[]> FolderData => new[]
        {
            // Folder name, is public, content type, cache control
            new object[] { CoreConstants.Folders.ContentFolderName, false, CoreConstants.JsonContentType, null },
            new object[] { CoreConstants.Folders.DownloadsFolderName, true, CoreConstants.OctetStreamContentType, null },
            new object[] { CoreConstants.Folders.PackageBackupsFolderName, true, CoreConstants.PackageContentType, null },
            new object[] { CoreConstants.Folders.PackageReadMesFolderName, false, CoreConstants.TextContentType, null },
            new object[] { CoreConstants.Folders.PackagesFolderName, true, CoreConstants.PackageContentType, CoreConstants.DefaultCacheControl },
            new object[] { CoreConstants.Folders.SymbolPackagesFolderName, true, CoreConstants.PackageContentType, CoreConstants.DefaultCacheControl },
            new object[] { CoreConstants.Folders.SymbolPackageBackupsFolderName, true, CoreConstants.PackageContentType, null },
            new object[] { CoreConstants.Folders.UploadsFolderName, false, CoreConstants.PackageContentType, null },
            new object[] { CoreConstants.Folders.UserCertificatesFolderName, false, CoreConstants.CertificateContentType, null },
            new object[] { CoreConstants.Folders.ValidationFolderName, false, CoreConstants.PackageContentType, CoreConstants.DefaultCacheControl },
            new object[] { CoreConstants.Folders.PackagesContentFolderName, false, CoreConstants.OctetStreamContentType, null },
            new object[] { CoreConstants.Folders.RevalidationFolderName, false, CoreConstants.JsonContentType, null },
            new object[] { CoreConstants.Folders.StatusFolderName, false, CoreConstants.JsonContentType, null },
            new object[] { CoreConstants.Folders.FlatContainerFolderName, true, CoreConstants.PackageContentType, null },
        };

        [Fact]
        public void FolderDataContainsAllFolders()
        {
            var folderNameFields = typeof(CoreConstants.Folders)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly).ToList();

            var folderNames = FolderData.Select(a => (string)a[0]).ToList();

            Assert.Equal(folderNameFields.Count, folderNames.Count);

            foreach (var folderNameField in folderNameFields)
            {
                var folderName = (string)folderNameField.GetRawConstantValue();
                Assert.Contains(folderName, folderNames);
            }
        }

        [Theory]
        [MemberData(nameof(FolderData))]
        public void ProducesExpectedOutput(string folderName, bool isPublic, string contentType, string cacheControl)
        {
            var target = new GalleryCloudBlobFolderDescription();

            Assert.Equal(isPublic, target.IsPublicContainer(folderName));
            Assert.Equal(contentType, target.GetContentType(folderName));
            Assert.Equal(cacheControl, target.GetCacheControl(folderName));
        }
    }
}
