// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// Used to ensure that only one instance of this service is running at once.
    /// </summary>
    public interface ISingletonService
    {
        /// <summary>
        /// Determines whether this is the only instance of the service running.
        /// </summary>
        /// <returns>True if this service is the only instance of the service running.</returns>
        Task<bool> IsSingletonAsync();
    }
}
