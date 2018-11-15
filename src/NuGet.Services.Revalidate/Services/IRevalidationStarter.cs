// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// The service used to try to start revalidations.
    /// </summary>
    public interface IRevalidationStarter
    {
        /// <summary>
        /// Attempt to start a single revalidation. This may choose not to enqueue
        /// a revalidation if:
        /// 
        /// 1. The ingestion pipeline appears unhealthy
        /// 2. The ingestion pipeline is too active
        /// 3. The revalidation job has been killswitched
        /// 4. A revalidation could not be found at this time
        /// </summary>
        /// <returns>The result of the revalidation attempt.</returns>
        Task<StartRevalidationResult> StartNextRevalidationsAsync();
    }
}
