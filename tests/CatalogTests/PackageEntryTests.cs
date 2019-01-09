// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class PackageEntryTests
    {
        [Fact]
        public void DefaultConstructor_InitializesDefaultValues()
        {
            var packageEntry = new PackageEntry();

            Assert.Null(packageEntry.FullName);
            Assert.Null(packageEntry.Name);
            Assert.Equal(0, packageEntry.CompressedLength);
            Assert.Equal(0, packageEntry.Length);
        }

        [Fact]
        public void Constructor_WhenZipArchiveEntryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PackageEntry(zipArchiveEntry: null));

            Assert.Equal("zipArchiveEntry", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithValidArguments_InitializesInstance()
        {
            using (var zipArchive = CreateZipArchive())
            {
                var zipArchiveEntry = zipArchive.Entries[0];

                var packageEntry = new PackageEntry(zipArchiveEntry);

                Assert.Equal(zipArchiveEntry.FullName, packageEntry.FullName);
                Assert.Equal(zipArchiveEntry.Name, packageEntry.Name);
                Assert.Equal(zipArchiveEntry.CompressedLength, packageEntry.CompressedLength);
                Assert.Equal(zipArchiveEntry.Length, packageEntry.Length);
            }
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var packageEntry = new PackageEntry()
            {
                FullName = "a/b",
                Name = "b",
                CompressedLength = 2,
                Length = 1
            };

            var json = JsonConvert.SerializeObject(packageEntry);

            Assert.Equal("{\"fullName\":\"a/b\",\"name\":\"b\",\"length\":1,\"compressedLength\":2}", json);
        }

        [Fact]
        public void JsonDeserialization_ReturnsCorrectObject()
        {
            var json = "{\"fullName\":\"a/b\",\"name\":\"b\",\"length\":1,\"compressedLength\":2}";

            var packageEntry = JsonConvert.DeserializeObject<PackageEntry>(json);

            Assert.Equal("a/b", packageEntry.FullName);
            Assert.Equal("b", packageEntry.Name);
            Assert.Equal(1, packageEntry.Length);
            Assert.Equal(2, packageEntry.CompressedLength);
        }

        private static ZipArchive CreateZipArchive()
        {
            var archiveStream = new MemoryStream();

            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("a/b.c", CompressionLevel.Optimal);

                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write("peach");
                }
            }

            return new ZipArchive(archiveStream, ZipArchiveMode.Read);
        }
    }
}