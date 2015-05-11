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
        public bool FriendlyLicenses { get; set; }
    }
}