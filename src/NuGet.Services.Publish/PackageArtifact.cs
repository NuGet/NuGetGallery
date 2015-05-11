// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System.IO;

namespace NuGet.Services.Publish
{
    public class PackageArtifact
    {
        public Stream Stream { get; set; }
        public string Location { get; set; }
        public JObject PackageEntry { get; set; }
    }
}