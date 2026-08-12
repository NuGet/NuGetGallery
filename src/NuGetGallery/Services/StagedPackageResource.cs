// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// The public staged package resource: one package ID and version that may carry package content (.nupkg),
    /// symbols content (.snupkg), or both. It is the aggregate users manage through the staging API.
    /// </summary>
    public class StagedPackageResource
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Owner { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public StagingGroupReferenceResource Group { get; set; }
        public string Status { get; set; }
        public bool CanPromote { get; set; }
        public IReadOnlyList<StagingBlockerResource> Blockers { get; set; }
        public StagingArtifactResource Package { get; set; }
        public StagingArtifactResource Symbols { get; set; }
        public StagingPromotionResource Promotion { get; set; }
        public string GalleryUrl { get; set; }
    }

    public class StagingArtifactResource
    {
        public string Source { get; set; }
        public string Status { get; set; }
        public DateTime? Uploaded { get; set; }
        public DateTime? Validated { get; set; }
        public string Operation { get; set; }
        public IReadOnlyList<StagingValidationErrorResource> Errors { get; set; }
    }

    public class StagingBlockerResource
    {
        public string Code { get; set; }
        public string Artifact { get; set; }
        public string Message { get; set; }
    }

    public class StagingValidationErrorResource
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public class StagingGroupReferenceResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class StagingPromotionResource
    {
        public string Status { get; set; }
        public DateTime? Started { get; set; }
        public string GalleryUrl { get; set; }
    }
}
