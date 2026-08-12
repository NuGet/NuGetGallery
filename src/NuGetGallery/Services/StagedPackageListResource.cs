// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// A page of staged packages owned by the credential owner, along with the owner's artifact quota.
    /// </summary>
    public class StagedPackageListResource
    {
        public string Owner { get; set; }
        public StagingQuotaResource Quota { get; set; }
        public IReadOnlyList<StagedPackageResource> Packages { get; set; }
        public string ContinuationToken { get; set; }
    }

    public class StagingQuotaResource
    {
        public int UsedArtifacts { get; set; }
        public int Limit { get; set; }
    }
}
