// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IRelatedPackagesService
    {
        /// <summary>
        /// Gets a list of packages related to <see cref="package"/>.
        /// </summary>
        Task<IEnumerable<Package>> GetRelatedPackagesAsync(Package package);
    }
}
