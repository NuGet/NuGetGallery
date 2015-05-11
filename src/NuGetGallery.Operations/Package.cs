// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
namespace NuGetGallery.Operations
{
    public class Package
    {
        public string Hash { get; set; }
        public string Id { get; set; }
        public int Key { get; set; }
        public string Version { get; set; }
        public string NormalizedVersion { get; set; }
        public string ExternalPackageUrl { get; set; }
        public DateTime? Created { get; set; }
    }
}
