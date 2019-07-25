// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileMetadata
    {
        [JsonConstructor]
        public AuxiliaryFileMetadata(
            DateTimeOffset lastModified,
            DateTimeOffset loaded,
            TimeSpan loadDuration,
            long fileSize,
            string etag)
        {
            Loaded = loaded;
            LastModified = lastModified;
            LoadDuration = loadDuration;
            FileSize = fileSize;
            ETag = etag ?? throw new ArgumentNullException(nameof(etag));
        }

        public DateTimeOffset LastModified { get; }
        public DateTimeOffset Loaded { get; }
        public TimeSpan LoadDuration { get; }
        public long FileSize { get; }
        public string ETag { get; }
    }
}
