// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public interface ISearchServiceClient
    {
        Task<DateTimeOffset> GetCommitDateTimeAsync(
            Instance instance,
            CancellationToken token);

        Task<DateTimeOffset> GetIndexLastReloadTimeAsync(
            Instance instance,
            CancellationToken token);

        Task<SearchResultResponse> GetSearchResultAsync(
            Instance instance,
            string query,
            CancellationToken token);

        Task<SearchResultResponse> GetResultForPackageIdVersion(
            Instance instance,
            string packageId,
            string packageVersion,
            CancellationToken token);

        Task<SearchDiagnosticResponse> GetSearchDiagnosticResponseAsync(
            Instance instance,
            CancellationToken token);

        IReadOnlyList<Instance> GetSearchEndpoints(RegionInformation regionInformation);
    }
}