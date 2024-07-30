// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace StatusAggregator.Container
{
    /// <summary>
    /// Simple wrapper for <see cref="CloudBlobContainer"/> that exists for unit-testing.
    /// </summary>
    public interface IContainerWrapper
    {
        Task CreateIfNotExistsAsync();

        Task SaveBlobAsync(string name, string contents);
    }
}