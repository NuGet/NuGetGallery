// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class NuGetExeDownloaderServiceFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/nuget.exe");
        private static readonly Uri HttpsRequestUrl = new Uri("https://nuget.org/nuget.exe");

        [Fact]
        public async Task CreateNuGetExeDownloadDoesNotExtractFileIfItAlreadyExists()
        {
            // Arrange
            var fileStorage = new Mock<IFileStorageService>(MockBehavior.Strict);
            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync(HttpRequestUrl, "downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            // Act
            var downloaderService = GetDownloaderService(fileStorageService: fileStorage);
            await downloaderService.CreateNuGetExeDownloadActionResultAsync(HttpRequestUrl);

            // Assert
            fileStorage.Verify();
        }

        private static NuGetExeDownloaderService GetDownloaderService(
            Mock<IFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<IFileStorageService>(MockBehavior.Strict);

            return new NuGetExeDownloaderService(fileStorageService.Object);
        }
    }
}