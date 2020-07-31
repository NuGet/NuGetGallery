// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class CatalogPackageEntry
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CompressedLength)]
        internal long CompressedLength { get; }
        [JsonProperty(CatalogConstants.FullName)]
        internal string FullName { get; }
        [JsonProperty(CatalogConstants.Length)]
        internal long Length { get; }
        [JsonProperty(CatalogConstants.Name)]
        internal string Name { get; }

        [JsonConstructor]
        internal CatalogPackageEntry(
            string idKeyword,
            string typeKeyword,
            long compressedLength,
            string fullName,
            long length,
            string name)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CompressedLength = compressedLength;
            FullName = fullName;
            Length = length;
            Name = name;
        }
    }
}