// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using Moq;
using NuGet.Services.Testing.Entities;

namespace System.Data.Entity
{
    public static class IDbSetMockExtensions
    {
        public static void SetupDbSet<TContext, TDbSet, TEntity>(
            this Mock<TContext> entityContext,
            Expression<Func<TContext, TDbSet>> dbSetAccessor,
            Mock<TDbSet> dbSet,
            IEnumerable<TEntity> dataEnumerable)
          where TContext : class
          where TDbSet : class, IDbSet<TEntity>
          where TEntity : class
        {
            dbSet = dbSet.SetupDbSet(dataEnumerable);
            entityContext.Setup(dbSetAccessor).Returns(dbSet.Object);
        }

        public static Mock<TDbSet> SetupDbSet<TDbSet, TEntity>(
            this Mock<TDbSet> dbSet,
            IEnumerable<TEntity> dataEnumerable)
          where TDbSet : class, IDbSet<TEntity>
          where TEntity : class
        {
            dbSet = dbSet ?? new Mock<TDbSet>();
            dataEnumerable = dataEnumerable ?? new TEntity[0];

            var data = dataEnumerable.AsQueryable();

            dbSet
                .As<IDbAsyncEnumerable<TEntity>>()
                .Setup(m => m.GetAsyncEnumerator())
                .Returns(() => new TestDbAsyncEnumerator<TEntity>(data.GetEnumerator()));

            dbSet
                .As<IQueryable<TEntity>>()
                .Setup(m => m.Provider)
                .Returns(() => new TestDbAsyncQueryProvider<TEntity>(data.Provider));

            dbSet
                .Setup(s => s.Add(It.IsAny<TEntity>()))
                .Callback<TEntity>(e => data = data.Concat(new[] { e }).AsQueryable());

            dbSet
                .Setup(s => s.Remove(It.IsAny<TEntity>()))
                .Callback<TEntity>(e => data = data.Where(d => e != d).AsQueryable());

            dbSet.As<IQueryable<TEntity>>().Setup(m => m.Expression).Returns(() => data.Expression);
            dbSet.As<IQueryable<TEntity>>().Setup(m => m.ElementType).Returns(() => data.ElementType);
            dbSet.As<IQueryable<TEntity>>().Setup(m => m.GetEnumerator()).Returns(() => data.GetEnumerator());

            return dbSet;
        }
    }
}
