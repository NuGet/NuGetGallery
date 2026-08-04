// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagingPromotionArtifactHistory : IEntity
    {
        public int Key { get; set; }

        public int StagingPromotionHistoryKey { get; set; }

        public StagingPromotionHistory StagingPromotionHistory { get; set; }

        public int? PackageKey { get; set; }

        public Package Package { get; set; }

        [Required]
        [StringLength(Constants.MaxPackageIdLength)]
        public string PackageId { get; set; }

        [Required]
        [StringLength(Constants.MaxPackageVersionLength)]
        public string NormalizedVersion { get; set; }

        public StagingPromotionArtifactHistoryKind Kind { get; set; }

        [Required]
        [StringLength(256)]
        public string ContentHash { get; set; }

        public Guid ValidationTrackingId { get; set; }

        public int? SymbolPackageKey { get; set; }

        public SymbolPackage SymbolPackage { get; set; }

        public Guid? CurrentIngestionValidationTrackingId { get; set; }

        public StagingPromotionArtifactHistoryStatus Status { get; set; }

        public DateTime? StartedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public int RetryCount { get; set; }

        public DateTime? LastRetryDate { get; set; }

        [StringLength(128)]
        public string LastErrorCode { get; set; }

        public DateTime? LastFailureDate { get; set; }

        public Guid? ProcessingLeaseId { get; set; }

        public DateTime? ProcessingLeaseExpiresDate { get; set; }

        public byte[] RowVersion { get; set; }
    }
}
