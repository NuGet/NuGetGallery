// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchStatusService
    {
        Task<SearchStatusResponse> GetStatusAsync(SearchStatusOptions options, Assembly assemblyForMetadata);
    }
}