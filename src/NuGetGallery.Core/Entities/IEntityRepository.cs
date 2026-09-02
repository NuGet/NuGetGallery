// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IEntityRepository<T> : IReadOnlyEntityRepository<T>
        where T : class, new()
    {
        /// <summary>
        /// Executes an action inside a database transaction and commits after the action completes successfully.
        /// </summary>
        /// <param name="action">The action to execute before committing the transaction.</param>
        /// <returns>A task that represents the transaction.</returns>
        Task ExecuteInTransactionAsync(Func<Task> action);

        Task CommitChangesAsync();
        void InsertOnCommit(T entity);
        void InsertOnCommit(IEnumerable<T> entities);
        void DeleteOnCommit(T entity);
        void DeleteOnCommit(IEnumerable<T> entities);
    }
}