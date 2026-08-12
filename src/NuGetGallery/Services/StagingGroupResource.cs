// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class StagingGroupResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public int PackageCount { get; set; }
        public int ArtifactCount { get; set; }
        public string Status { get; set; }
        public bool CanPromote { get; set; }
        public string GalleryUrl { get; set; }
    }

    public class StagingGroupDetailResource : StagingGroupResource
    {
        public IReadOnlyList<StagedPackageResource> Packages { get; set; }
        public string ContinuationToken { get; set; }
    }
}
