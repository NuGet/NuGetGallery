// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery
{
    public class FileStorageResultExtensionsFacts
    {
        [Fact]
        public void ConvertsNotFoundResult()
        {
            var result = new FileStorageResult.NotFound().ToActionResult();

            Assert.IsType<HttpNotFoundResult>(result);
        }

        [Fact]
        public void ConvertsRedirectResult()
        {
            var result = new FileStorageResult.Redirect(new Uri("https://example.test/file")).ToActionResult();

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://example.test/file", redirectResult.Url);
            Assert.False(redirectResult.Permanent);
        }

        [Fact]
        public void ConvertsFilePathResult()
        {
            var result = new FileStorageResult.FilePath("path", "content/type", "download.name").ToActionResult();

            var filePathResult = Assert.IsType<FilePathResult>(result);
            Assert.Equal("path", filePathResult.FileName);
            Assert.Equal("content/type", filePathResult.ContentType);
            Assert.Equal("download.name", filePathResult.FileDownloadName);
        }
    }
}
