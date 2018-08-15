// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// Starts revalidations until there are no more pending revalidations.
    /// </summary>
    public interface IRevalidationService
    {
        /// <summary>
        /// Start revalidations until there are no more pending revalidations.
        /// </summary>
        /// <returns></returns>
        Task RunAsync();
    }
}
