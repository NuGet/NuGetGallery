// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ModifyFeatureFlagsFeatureViewModel 
        : FeatureFlagsFeatureViewModel, IModifyFeatureFlagsViewModel<FeatureFlagsFeatureViewModel>
    {
        public ModifyFeatureFlagsFeatureViewModel()
        {
        }

        public ModifyFeatureFlagsFeatureViewModel(
            FeatureFlagsFeatureViewModel feature,
            string contentId)
            : base(feature)
        {
            ContentId = contentId;
        }

        [Required]
        public string ContentId { get; set; }

        public string PrettyName => "feature";

        public List<FeatureFlagsFeatureViewModel> GetExistingList(FeatureFlagsViewModel model)
        {
            return model.Features;
        }

        /// <remarks>
        /// Features have no validations.
        /// </remarks>
        public string GetValidationError(IUserService userService)
        {
            return null;
        }

        public void ApplyTo(FeatureFlagsFeatureViewModel target)
        {
            target.Status = Status;
        }
    }
}