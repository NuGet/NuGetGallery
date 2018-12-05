// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Packaging;

namespace NuGetGallery
{
    public class TestPackageReader
        : PackageArchiveReader
    {
        private readonly Stream _stream;

        public TestPackageReader(Stream stream) 
            : base(stream)
        {
            _stream = stream;
        }

        public Stream GetStream()
        {
            return _stream;
        }

        public new virtual IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            return base.GetPackageDependencies();
        }
    }
}