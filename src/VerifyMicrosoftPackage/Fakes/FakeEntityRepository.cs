// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.VerifyMicrosoftPackage.Fakes
{
    public class FakeEntityRepository<T> : IEntityRepository<T> where T : class, new()
    {
        private readonly List<T> _entities;

        public FakeEntityRepository(params T[] entities)
        {
            _entities = entities.ToList();
        }

        public Task CommitChangesAsync()
        {
            return Task.CompletedTask;
        }

        public void DeleteOnCommit(T entity)
        {
        }

        public void DeleteOnCommit(IEnumerable<T> entities)
        {
        }

        public IQueryable<T> GetAll()
        {
            return _entities.AsQueryable();
        }

        public void InsertOnCommit(T entity)
        {
        }

        public void InsertOnCommit(IEnumerable<T> entities)
        {
        }
    }
}
