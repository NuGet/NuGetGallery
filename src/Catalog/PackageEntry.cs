// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Compression;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog
{
    public class PackageEntry
    {
        public PackageEntry()
        {
        }

        public PackageEntry(ZipArchiveEntry zipArchiveEntry)
        {
            if (zipArchiveEntry == null)
            {
                throw new ArgumentNullException(nameof(zipArchiveEntry));
            }

            FullName = zipArchiveEntry.FullName;
            Name = zipArchiveEntry.Name;
            Length = zipArchiveEntry.Length;
            CompressedLength = zipArchiveEntry.CompressedLength;
        }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("length")]
        public long Length { get; set; }

        [JsonProperty("compressedLength")]
        public long CompressedLength { get; set; }
    }
}