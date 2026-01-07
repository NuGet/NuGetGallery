// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NuGet.Packaging;

namespace NuGetGallery.TestUtils
{
    class MockPackageArchiveReader : PackageArchiveReader
    {
        private NuspecReader _nuspecReader;
        private IEnumerable<string> _files;

        public MockPackageArchiveReader(NuspecReader nuspecReader, IEnumerable<string> files)
            : base(new ZipArchive(new MemoryStream(), ZipArchiveMode.Create, true))
        {
            _nuspecReader = nuspecReader;
            _files = files;
        }

        public override NuspecReader NuspecReader => _nuspecReader;
        public override IEnumerable<string> GetFiles() => _files;
    }
}