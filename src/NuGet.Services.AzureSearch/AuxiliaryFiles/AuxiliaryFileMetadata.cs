// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileMetadata
    {
        [JsonConstructor]
        public AuxiliaryFileMetadata(
            DateTimeOffset lastModified,
            TimeSpan loadDuration,
            long fileSize,
            string etag)
        {
            LastModified = lastModified;
            LoadDuration = loadDuration;
            FileSize = fileSize;
            ETag = etag ?? throw new ArgumentNullException(nameof(etag));
        }

        public DateTimeOffset LastModified { get; }
        public TimeSpan LoadDuration { get; }
        public long FileSize { get; }
        public string ETag { get; }

        public IAccessCondition GetIfMatchCondition()
        {
            return AccessConditionWrapper.GenerateIfMatchCondition(ETag);
        }
    }
}
