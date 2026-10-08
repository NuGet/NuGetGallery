// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery
{
    public class PackageStagingViewModel
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Owner { get; set; }

        public bool IsSymbolPackage { get; set; }

        public string ParentStatus { get; set; }

        public string ParentUrl { get; set; }

        public string Status { get; set; }

        public string StatusClass { get; set; }

        public DateTime UploadedDate { get; set; }

        /// <summary>
        /// Gets or sets the effective artifact or group expiration deadline.
        /// </summary>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets whether the artifact is logically expired and awaiting cleanup.
        /// </summary>
        public bool IsExpired { get; set; }

        public IReadOnlyList<ValidationIssue> ValidationIssues { get; set; }

        public bool Listed { get; set; }

        public bool CanManage { get; set; }

        public bool CanPromote { get; set; }

        public bool ReplacesPublishedSymbols { get; set; }

        public bool IncludesStagedSymbols { get; set; }

        public bool CanResend { get; set; }

        public string PromotionBlocker { get; set; }

        public string MoveUrl { get; set; }
    }
}
