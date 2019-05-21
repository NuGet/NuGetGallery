// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGetGallery.Areas.Admin.Services
{
    public class ValidationEntityRepository<T> : IEntityRepository<T>
        where T : class, new()
    {
        private readonly ValidationEntitiesContext _entities;

        public ValidationEntityRepository(ValidationEntitiesContext entities)
        {
            _entities = entities;
        }

        public async Task CommitChangesAsync()
        {
            await _entities.SaveChangesAsync();
        }

        public void InsertOnCommit(T entity)
        {
            _entities.Set<T>().Add(entity);
        }

        public void InsertOnCommit(IEnumerable<T> entities)
        {
            _entities.Set<T>().AddRange(entities);
        }

        public void DeleteOnCommit(T entity)
        {
            _entities.Set<T>().Remove(entity);
        }

        public void DeleteOnCommit(IEnumerable<T> entities)
        {
            _entities.Set<T>().RemoveRange(entities);
        }

        public IQueryable<T> GetAll()
        {
            return _entities.Set<T>();
        }

        public IDatabase GetDatabase()
        {
            throw new NotImplementedException();
        }
    }
}