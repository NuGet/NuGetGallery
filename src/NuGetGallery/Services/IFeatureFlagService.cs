// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Whether typosquatting detection is enabled on package uploads. If true, new packages
        /// cannot have an id that is similar to existing packages' ids.
        /// </summary>
        /// <returns></returns>
        bool IsTyposquattingEnabled();

        /// <summary>
        /// Whether typosquatting detection is enabled for a specific user's package uploads. If true,
        /// new packages from this user cannot have an id that is similar to existing packages' ids.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        bool IsTyposquattingEnabled(User user);
    }
}
