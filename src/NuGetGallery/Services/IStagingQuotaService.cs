// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Reports owner staging usage and checks capacity for one additional private artifact.
    /// </summary>
    public interface IStagingQuotaService
    {
        StagingQuotaUsage GetUsage(User owner);

        Task EnsureCapacityAsync(User owner);
    }
}
