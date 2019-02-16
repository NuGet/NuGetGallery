// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsViewModel
    {
        public TimeSpan RefreshInterval { get; set; }

        public TimeSpan? TimeSinceLastRefresh { get; set; }

        public bool IsTimeSinceLastRefreshTooHigh
        {
            // Ideally, the feature flags refresh instantly at the refresh interval.
            // To account for random delays, we'll allow for up to a minute of drift before
            // showing a warning.
            get => TimeSinceLastRefresh > RefreshInterval.Add(TimeSpan.FromMinutes(1));
        }

        [Required]
        public string Flags { get; set; }

        [Required]
        public string ContentId { get; set; }
    }
}