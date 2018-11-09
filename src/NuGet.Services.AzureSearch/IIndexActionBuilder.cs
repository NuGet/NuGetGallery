// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.AzureSearch.Db2AzureSearch;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Produces <see cref="IndexAction{T}"/> from incoming package data. It is the caller's responsibility to batch
    /// and submit these actions to Azure Search.
    /// </summary>
    public interface IIndexActionBuilder
    {
        /// <summary>
        /// Build the index actions required to add a new package registration. This method is used by db2azuresearch
        /// since an entire package registration is known.
        /// </summary>
        /// <param name="packageRegistration">The package registration and its packages.</param>
        /// <returns>The index actions to send to Azure Search.</returns>
        IndexActions AddNewPackageRegistration(NewPackageRegistration packageRegistration);
    }
}