// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Records a private symbol validation attempt and its immutable uploaded content reference.
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

        /// <summary>
        /// Identifies the accepted promotion of this immutable attempt.
        /// </summary>
        public Guid? ActivePromotionId { get; set; }

        /// <summary>
        /// Records the last dispatch of an individual promotion.
        /// </summary>
        public DateTime? PromotionMessageSentDate { get; set; }

        /// <summary>
        /// Forces a concurrency check when the staging identity changes groups.
        /// </summary>
        public int MutationRevision { get; set; }

        public byte[] RowVersion { get; set; }
    }
}
