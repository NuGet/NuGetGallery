// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class NupkgMetadata
    {
        public XDocument Nuspec { get; }
        public IEnumerable<PackageEntry> Entries { get; }
        public long PackageSize { get; }
        public string PackageHash { get; }

        public NupkgMetadata(XDocument nuspec, IEnumerable<PackageEntry> entries, long packageSize, string packageHash)
        {
            Nuspec = nuspec;
            Entries = entries;
            PackageSize = packageSize;
            PackageHash = packageHash;
        }
    }
}