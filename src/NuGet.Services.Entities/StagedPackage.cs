// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagedPackage : IEntity
    {
        public int Key { get; set; }

        public int PackageKey { get; set; }

        public virtual Package Package { get; set; }

        public int OwnerKey { get; set; }

        public virtual User Owner { get; set; }

        [Required]
        [StringLength(256)]
        public string UploadedBlobPath { get; set; }

        [Required]
        [StringLength(256)]
        public string UploadedBlobETag { get; set; }

        [StringLength(256)]
        public string ValidatedBlobPath { get; set; }

        [StringLength(256)]
        public string ValidatedBlobETag { get; set; }

        [Required]
        [StringLength(256)]
        public string UploadHash { get; set; }

        public StagedPackageStatus Status { get; set; }

        public DateTime UploadedDate { get; set; }
    }
}
