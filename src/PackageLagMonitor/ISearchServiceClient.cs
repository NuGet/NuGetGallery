// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public interface ISearchServiceClient
    {
        Task<DateTimeOffset> GetCommitDateTimeAsync(
            Instance instance,
            CancellationToken token);

        Task<IReadOnlyList<Instance>> GetSearchEndpointsAsync(
            RegionInformation regionInformation,
            CancellationToken token);
    }
}