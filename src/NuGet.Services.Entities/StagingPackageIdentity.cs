// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents the shared staging state for a package identity and its artifact attempts.
    /// </summary>
    public class StagingPackageIdentity : IEntity
    {
        public int Key { get; set; }

        public virtual Package Package { get; set; }

        public int OwnerKey { get; set; }

        public virtual User Owner { get; set; }

        public int? StagingGroupKey { get; set; }

        public virtual StagingGroup StagingGroup { get; set; }

        public int? CurrentStagedPackageKey { get; set; }

        public virtual StagedPackage CurrentStagedPackage { get; set; }
    }
}
