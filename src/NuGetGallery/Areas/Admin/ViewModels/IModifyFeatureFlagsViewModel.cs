// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public interface IModifyFeatureFlagsViewModel<TBase> : IFeatureFlagsObjectViewModel
        where TBase : IFeatureFlagsObjectViewModel
    {
        string ContentId { get; set; }

        string PrettyName { get; }
        void ApplyTo(TBase target);
        List<TBase> GetExistingList(FeatureFlagsViewModel model);
        string GetValidationError(IUserService userService);
    }
}
