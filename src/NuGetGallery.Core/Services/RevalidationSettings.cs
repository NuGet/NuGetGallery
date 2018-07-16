// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The settings for the revalidation job.
    /// </summary>
    public class RevalidationSettings
    {
        /// <summary>
        /// Whether the revalidation job has been disactivated.
        /// </summary>
        public bool Killswitch { get; set; } = false;

        /// <summary>
        /// Whether the revalidation job's state has been initialized.
        /// </summary>
        public bool Initialized { get; set; } = false;

        /// <summary>
        /// The desired number maximal number of package events (pushes, lists, unlists, revalidations)
        /// per hour.
        /// </summary>
        public int DesiredPackageEventRate { get; set; }
    }
}
