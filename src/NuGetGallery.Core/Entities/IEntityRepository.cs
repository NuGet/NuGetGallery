// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IEntityRepository<T>
        where T : class, new()
    {
        Task CommitChangesAsync();
        void DeleteOnCommit(T entity);
        IQueryable<T> GetAll();
        void InsertOnCommit(T entity);
    }
}