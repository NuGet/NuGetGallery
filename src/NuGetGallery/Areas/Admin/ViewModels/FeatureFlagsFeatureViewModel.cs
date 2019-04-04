// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsFeatureViewModel : IFeatureFlagsViewModel
    {
        public FeatureFlagsFeatureViewModel()
        {
        }

        public FeatureFlagsFeatureViewModel(
            FeatureFlagsFeatureViewModel feature)
        {
            Name = feature.Name;
            Status = feature.Status;
        }

        public FeatureFlagsFeatureViewModel(
            string name,
            FeatureStatus status)
        {
            Name = name;
            Status = status;
        }

        [Required]
        public string Name { get; set; }

        public FeatureStatus Status { get; set; }
    }
}