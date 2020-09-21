// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IABTestService
    {
        /// <summary>
        /// Whether or not the user should see the preview search implementation.
        /// </summary>
        bool IsPreviewSearchEnabled(User user);

        /// <summary>
        /// Whether or not the user should see the reverse dependencies implementation.
        /// </summary>
        bool IsPackageDependendentsABEnabled(User user);
    }
}