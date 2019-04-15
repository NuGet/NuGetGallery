// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface IAuxiliaryDataCache
    {
        /// <summary>
        /// Returns true if there is auxiliary data available. False, otherwise. If there is data available, it can be
        /// retrieved using <see cref="Get"/>.
        /// </summary>
        bool Initialized { get; }

        /// <summary>
        /// Returns the cached loaded auxiliary data.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there is not data available. <see cref="EnsureInitializedAsync"/> should be called if this is
        /// thrown. <see cref="Initialized"/> can be used to check whether data is available.
        /// </exception>
        IAuxiliaryData Get();

        /// <summary>
        /// Load the latest version of the auxiliary data if it is not already loaded. If the data is already being
        /// loaded by another caller, this method waits until that other reload finishes and ensures that the data was
        /// loaded. If the other caller successfully loaded the auxiliary data, this method will no-op.
        /// </summary>
        Task EnsureInitializedAsync();

        /// <summary>
        /// Tries to load the latest version of the auxiliary data. If the data is already being loaded by another
        /// caller, this method does not reload the data but does wait until some data is available.
        /// </summary>
        Task TryLoadAsync(CancellationToken token);
    }
}