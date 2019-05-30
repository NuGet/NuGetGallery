// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IEntityRepository<T> : IReadOnlyEntityRepository<T>
        where T : class, new()
    {
        Task CommitChangesAsync();
        void InsertOnCommit(T entity);
        void InsertOnCommit(IEnumerable<T> entities);
        void DeleteOnCommit(T entity);
        void DeleteOnCommit(IEnumerable<T> entities);
    }
}