// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class NupkgMetadata
    {
        public XDocument Nuspec { get; set; }
        public IEnumerable<PackageEntry> Entries { get; set; }
        public long PackageSize { get; set; }
        public string PackageHash { get; set; }
    }
}
