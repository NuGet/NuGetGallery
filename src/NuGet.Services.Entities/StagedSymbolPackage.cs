// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Records an immutable private symbol upload and its staging validation state.
    /// </summary>
    public class StagedSymbolPackage : IEntity
    {
        public int Key { get; set; }

        public int SymbolPackageKey { get; set; }

        public virtual SymbolPackage SymbolPackage { get; set; }

        public int StagedPackageIdentityKey { get; set; }

        public virtual StagedPackageIdentity StagedPackageIdentity { get; set; }

        [Required]
        [StringLength(256)]
        public string UploadedBlobPath { get; set; }

        [Required]
        [StringLength(256)]
        public string UploadedBlobETag { get; set; }

        public StagedPackageStatus Status { get; set; }

        public DateTime UploadedDate { get; set; }

        public byte[] RowVersion { get; set; }
    }
}
