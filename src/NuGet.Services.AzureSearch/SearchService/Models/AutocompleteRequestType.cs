// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public enum AutocompleteRequestType
    {
        /// <summary>
        /// The response's data should list matching package IDs.
        /// </summary>
        PackageIds,

        /// <summary>
        /// The response should list the package's versions.
        /// </summary>
        PackageVersions,
    }
}
