// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// Represents an owner-visible staging group and its current package attempts.
    /// </summary>
    public class StagingGroupDetailViewModel
    {
        public string Owner { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsUngrouped { get; set; }

        public bool IsPromotionActive { get; set; }

        public bool CanPromote { get; set; }

        public bool CanResend { get; set; }

        public int PackageCount { get; set; }

        public int ReadyCount { get; set; }

        public int ValidatingCount { get; set; }

        public int PromotingCount { get; set; }

        public int FailedCount { get; set; }

        public int PromotionFailedCount { get; set; }

        public IReadOnlyList<PackageStagingViewModel> Packages { get; set; }
    }
}
