// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Requests conditional deletion of an obsolete private staging file.
    /// </summary>
    public class StagingBlobCleanup : IEntity
    {
        public int Key { get; set; }

        /// <summary>
        /// Snapshots the identity key without a foreign key so cleanup survives identity deletion.
        /// </summary>
        public int StagedPackageIdentityKey { get; set; }

        [Required]
        [StringLength(256)]
        public string BlobPath { get; set; }

        [Required]
        [StringLength(256)]
        public string BlobETag { get; set; }

        public DateTime QueuedDate { get; set; }
    }
}
