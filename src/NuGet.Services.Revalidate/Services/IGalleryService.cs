// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public interface IGalleryService
    {
        /// <summary>
        /// Count the number of gallery events (package pushes, listing, and unlisting) in the past hour.
        /// </summary>
        /// <returns>The number of gallery events in the past hour.</returns>
        Task<int> CountEventsInPastHourAsync();
    }
}
