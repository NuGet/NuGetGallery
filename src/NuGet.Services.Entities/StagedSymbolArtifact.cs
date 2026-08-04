// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagedSymbolArtifact
    {
        public int StagingEntryKey { get; set; }

        public StagingEntry StagingEntry { get; set; }

        public int SymbolPackageKey { get; set; }

        public SymbolPackage SymbolPackage { get; set; }

        [Required]
        [StringLength(256)]
        public string BlobPath { get; set; }

        [Required]
        [StringLength(256)]
        public string BlobETag { get; set; }

        [Required]
        [StringLength(256)]
        public string ContentHash { get; set; }

        [StringLength(256)]
        public string ParentContentHash { get; set; }

        public StagingArtifactStatus Status { get; set; }

        public Guid ValidationTrackingId { get; set; }

        public DateTime UploadedDate { get; set; }

        public DateTime? ValidatedDate { get; set; }

        public int? PromotionArtifactHistoryKey { get; set; }

        public StagingPromotionArtifactHistory PromotionArtifactHistory { get; set; }

        public DateTime? PromotionStartedDate { get; set; }

        public byte[] RowVersion { get; set; }
    }
}
