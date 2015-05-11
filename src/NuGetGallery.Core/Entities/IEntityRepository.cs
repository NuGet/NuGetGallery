// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;

namespace NuGetGallery
{
    public interface IEntityRepository<T>
        where T : class, IEntity, new()
    {
        void CommitChanges();
        void DeleteOnCommit(T entity);
        T GetEntity(int key);
        IQueryable<T> GetAll();
        int InsertOnCommit(T entity);
    }
}