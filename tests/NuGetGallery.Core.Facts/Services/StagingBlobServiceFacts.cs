// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class StagingBlobServiceFacts
    {
        [Fact]
        public async Task SavesPackageToImmutablePath()
        {
            var content = new byte[] { 1, 2, 3 };
            var storage = new Mock<ICoreFileStorageService>();

            var result = await new StagingBlobService(storage.Object).SavePackageFileAsync(
                "NuGet.Versioning",
                "3.4.0",
                new MemoryStream(content));

            Assert.StartsWith("nuget.versioning/3.4.0/", result);
            Assert.EndsWith(".nupkg", result);
            storage.Verify(x => x.SaveFileAsync(
                CoreConstants.Folders.StagingFolderName,
                result,
                CoreConstants.PackageContentType,
                It.IsAny<Stream>(),
                false));
        }
    }
}
