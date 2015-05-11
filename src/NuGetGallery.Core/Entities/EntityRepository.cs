// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;

namespace NuGetGallery
{
    public class EntityRepository<T> : IEntityRepository<T>
        where T : class, IEntity, new()
    {
        private readonly IEntitiesContext _entities;

        public EntityRepository(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public void CommitChanges()
        {
            _entities.SaveChanges();
        }

        public void DeleteOnCommit(T entity)
        {
            _entities.Set<T>().Remove(entity);
        }

        public T GetEntity(int key)
        {
            return _entities.Set<T>().Find(key);
        }

        public IQueryable<T> GetAll()
        {
            return _entities.Set<T>();
        }

        public int InsertOnCommit(T entity)
        {
            _entities.Set<T>().Add(entity);

            return entity.Key;
        }
    }
}