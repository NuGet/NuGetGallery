// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class StagingFileReference
    {
        public StagingFileReference(string path, string etag)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ETag = etag ?? throw new ArgumentNullException(nameof(etag));
        }

        public string Path { get; }

        public string ETag { get; }
    }
}
