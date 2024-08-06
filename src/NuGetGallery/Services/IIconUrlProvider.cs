// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Abstracts the logic of getting the package's icon URL.
    /// </summary>
    public interface IIconUrlProvider
    {
        /// <summary>
        /// Produces the package icon URL as a string.
        /// </summary>
        /// <param name="package">Package for which icon URL is to be produced.</param>
        /// <returns>null if no icon URL can be produced, icon URL otherwise.</returns>
        string GetIconUrlString(Package package);

        /// <summary>
        /// Produces the package icon URL.
        /// </summary>
        /// <param name="package">Package for which icon URL is to be produced.</param>
        /// <returns>null if no icon URL can be produced, icon URL otherwise.</returns>
        Uri GetIconUrl(Package package);
    }
}
