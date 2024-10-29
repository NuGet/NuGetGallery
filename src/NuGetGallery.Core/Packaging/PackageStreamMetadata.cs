// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Packaging
{
    public class PackageStreamMetadata
    {
        public string HashAlgorithm { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
    }
}