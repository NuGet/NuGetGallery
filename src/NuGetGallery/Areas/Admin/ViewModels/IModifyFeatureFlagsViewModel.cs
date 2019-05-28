// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGetGallery.Services.UserManagement;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public interface IModifyFeatureFlagsViewModel<TBase> : IModifyFeatureFlagsViewModel
        where TBase : IFeatureFlagsObjectViewModel
    {
        void ApplyTo(TBase target);
        List<TBase> GetExistingList(FeatureFlagsViewModel model);
        string GetValidationError(IUserService userService);
    }

    public interface IModifyFeatureFlagsViewModel : IFeatureFlagsObjectViewModel
    {
        string ContentId { get; set; }

        string PrettyName { get; }
    }
}
