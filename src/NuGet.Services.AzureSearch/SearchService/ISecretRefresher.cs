// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Used to update the secrets in the background periodically.
    /// </summary>
    public interface ISecretRefresher
    {
        DateTimeOffset LastRefresh { get; }
        Task RefreshContinuouslyAsync(CancellationToken token);
    }
}
