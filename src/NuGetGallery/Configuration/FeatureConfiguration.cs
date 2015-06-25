// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

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
        /// Gets a boolean indicating if package restore via search is enabled.
        /// </summary>
        [DefaultValue(false)] // Default: Disabled
        [Description("Handles package restore (FIndPackagesById()) via search service")]
        public virtual bool PackageRestoreViaSearch { get; set; }
    }
}