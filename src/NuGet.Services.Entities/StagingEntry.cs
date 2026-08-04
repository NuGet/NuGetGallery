// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Entities
{
    public class StagingEntry : IEntity
    {
        public int Key { get; set; }

        public int PackageKey { get; set; }

        public Package Package { get; set; }

        /// <summary>
        /// Gets or sets the account that owns this staging work and pays its quota. Package authorization continues
        /// to use the potentially multiple owners on the package registration.
        /// </summary>
        public int OwnerKey { get; set; }

        /// <summary>
        /// Gets or sets the account identified by <see cref="OwnerKey"/>.
        /// </summary>
        public User Owner { get; set; }

        public int? StagingGroupKey { get; set; }

        public StagingGroup StagingGroup { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ExpirationDate { get; set; }

        public byte[] RowVersion { get; set; }

        public StagedPackageArtifact PackageArtifact { get; set; }

        public StagedSymbolArtifact SymbolArtifact { get; set; }
    }
}
