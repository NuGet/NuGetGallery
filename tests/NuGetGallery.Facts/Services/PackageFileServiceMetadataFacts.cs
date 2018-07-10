// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
namespace NuGetGallery
{
    public class PackageFileServiceMetadataFacts
    {
        [Fact]
        public void ValidatePropertyValues()
        {
            // Arrange + Act 
            var data = new PackageFileMetadataService();

            // Assert
            Assert.Equal("packages", data.FileFolderName);
            Assert.Equal("{0}.{1}{2}", data.FileSavePathTemplate);
            Assert.Equal(".nupkg", data.FileExtension);
            Assert.Equal("validation", data.ValidationFolderName);
            Assert.Equal("package-backups", data.FileBackupsFolderName);
            Assert.Equal("{0}/{1}/{2}.{3}", data.FileBackupSavePathTemplate);
        }
    }
}
