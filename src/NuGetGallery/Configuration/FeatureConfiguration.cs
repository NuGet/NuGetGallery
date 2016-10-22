// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel;

namespace NuGetGallery.Configuration
{
    public class FeatureConfiguration
    {
        /// <summary>
        /// Gets a boolean indicating if license reports are enabled.
        /// </summary>
        [DefaultValue(true)] // Default: Enabled
        [Description("Displays reports on license data")]
        public virtual bool FriendlyLicenses { get; set; }

        /// <summary>
        /// Gets a boolean indicating if package download counts should be recorded in the local database.
        /// </summary>
        [DefaultValue(false)] // Default: Disabled
        [Description("Indicates if package download counts should be recorded in the local database")]
        public virtual bool TrackPackageDownloadCountInLocalDatabase { get; set; }
    }
}