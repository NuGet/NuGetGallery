// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents an owner-visible staging group on the Manage Packages page.
    /// </summary>
    public class StagingGroupViewModel
    {
        public string Owner { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public DateTime CreatedDate { get; set; }

        public int PackageCount { get; set; }

        public string PackageStatusSummary { get; set; }

        public string Status { get; set; }

        public string StatusClass { get; set; }
    }
}
