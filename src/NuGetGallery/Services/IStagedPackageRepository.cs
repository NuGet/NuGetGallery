// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Persists staged packages and supports operations that require an explicit database transaction.
    /// </summary>
    public interface IStagedPackageRepository : IEntityRepository<StagedPackage>
    {
        /// <summary>
        /// Executes an action inside a database transaction and commits after the action completes successfully.
        /// </summary>
        /// <param name="action">The action to execute before committing the transaction.</param>
        /// <returns>A task that represents the transaction.</returns>
        Task ExecuteInTransactionAsync(Func<Task> action);
    }
}
