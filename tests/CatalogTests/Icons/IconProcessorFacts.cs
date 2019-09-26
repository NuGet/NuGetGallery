// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Icons
{
    public class IconProcessorFacts
    {
        public class TheCopyIconFromExternalSourceMethod : TestBase
        {
            [Fact]
            public async Task ReadsAndUsesStreamData()
            {
                var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xAA };

                using (var ms = new MemoryStream(data))
                {
                    await Target.CopyIconFromExternalSource(ms, DestinationStorageMock.Object, "somePath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(ds => ds.SaveAsync(
                    It.IsAny<Uri>(),
                    It.Is<StorageContent>(sc => SameData(data, sc)),
                    It.IsAny<CancellationToken>()));
            }

            [Theory]
            [MemberData(nameof(ImageData))]
            public async Task DeterminesContentType(byte[] data, string expectedContentType)
            {
                using (var ms = new MemoryStream(data))
                {
                    await Target.CopyIconFromExternalSource(ms, DestinationStorageMock.Object, "somePath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(ds => ds.SaveAsync(
                    It.IsAny<Uri>(),
                    It.Is<StorageContent>(sc => expectedContentType == sc.ContentType),
                    It.IsAny<CancellationToken>()));
            }
        }

        public class TheCopyEmbeddedIconFromPackageMethod : TestBase
        {
            [Fact]
            public async Task NoOpsIfIconFileDoesNotExist()
            {
                using (var packageStream = PrepareZippedImage("icon.xyz", new byte[] { 0xFF, 0xD8, 0xFF, 0x21, 0x17 }))
                {
                    await Target.CopyEmbeddedIconFromPackage(packageStream, "icon.foo", DestinationStorageMock.Object, "somepath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(ds => ds.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            public static IEnumerable<object[]> ExtractsAndSavesIconData =>
                from d in ImageData
                select new object[] 
                {
                    d[0],
                    (string)d[1] == "image/jpeg" || (string)d[1] == "image/png" ? d[1] : string.Empty
                };

            [Theory]
            [MemberData(nameof(ExtractsAndSavesIconData))]
            public async Task ExtractsAndSavesIcon(byte[] imageData, string expectedContentType)
            {
                const string iconFilename = "somefile.sxt";
                var destinationUri = new Uri("https://nuget.test/somepath");
                DestinationStorageMock
                    .Setup(ds => ds.ResolveUri("somepath"))
                    .Returns(destinationUri);
                using (var packageStream = PrepareZippedImage(iconFilename, imageData))
                {
                    await Target.CopyEmbeddedIconFromPackage(packageStream, iconFilename, DestinationStorageMock.Object, "somepath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(
                    ds => ds.SaveAsync(
                        It.Is<Uri>(u => u == destinationUri),
                        It.Is<StorageContent>(sc => SameDataAndContentType(imageData, expectedContentType, sc)),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            private static bool SameDataAndContentType(byte[] expectedData, string expectedContentType, StorageContent content)
            {
                return SameData(expectedData, content) && content.ContentType == expectedContentType;
            }

            private static MemoryStream PrepareZippedImage(string imagePath, byte[] imageData)
            {
                var result = new MemoryStream();

                using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = archive.CreateEntry(imagePath);
                    using(var entryStream = entry.Open())
                    {
                        entryStream.Write(imageData, 0, imageData.Length);
                    }
                }

                result.Seek(0, SeekOrigin.Begin);
                return result;
            }
        }

        public class TestBase
        {
            protected IconProcessor Target { get; set; }
            protected Mock<IStorage> DestinationStorageMock { get; private set; }
            protected Mock<ITelemetryService> TelemetryServiceMock { get; set; }
            protected Mock<ILogger<IconProcessor>> LoggerMock { get; set; }

            public TestBase()
            {
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<IconProcessor>>();

                Target = new IconProcessor(TelemetryServiceMock.Object, LoggerMock.Object);

                DestinationStorageMock = new Mock<IStorage>();
            }

            public static IEnumerable<object[]> ImageData = new[] {
                new object[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x42 }, "image/png" },
                new object[] { new byte[] { 0xFF, 0xD8, 0xFF, 0x21, 0x17 }, "image/jpeg" },
                new object[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x34, 0x12 }, "image/gif" },
                new object[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x45, 0x98, 0x03 }, "image/gif" },
                new object[] { new byte[] { 0x00, 0x00, 0x01, 0x00, 0x92 }, "image/x-icon" },
                new object[] { Encoding.UTF8.GetBytes("<svg></svg>"), "image/svg+xml" }
            };

            protected static bool SameData(byte[] data, StorageContent storageContent)
            {
                using (var dataStream = storageContent.GetContentStream())
                using (var m = new MemoryStream())
                {
                    dataStream.CopyTo(m);
                    var submittedArray = m.ToArray();
                    return data.SequenceEqual(submittedArray);
                }
            }
        }
    }
}
