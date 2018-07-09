// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public interface IHealthService
    {
        /// <summary>
        /// Determine whether the NuGet service is healthy.
        /// </summary>
        /// <returns>Whether the NuGet service is healthy.</returns>
        Task<bool> IsHealthyAsync();
    }
}
