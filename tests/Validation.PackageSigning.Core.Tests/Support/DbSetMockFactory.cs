// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;

namespace Validation.PackageSigning.Core.Tests.Support
{
    public static class DbSetMockFactory
    {
        public static IDbSet<T> Create<T>(params T[] sourceList) where T : class
        {
            var list = new List<T>(sourceList);

            var dbSet = new Mock<IDbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(() => new TestDbAsyncQueryProvider<T>(list.AsQueryable().Provider));
            dbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(() => list.AsQueryable().Expression);
            dbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(() => list.AsQueryable().ElementType);
            dbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => list.GetEnumerator());
            dbSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(e => list.Add(e));

            return dbSet.Object;
        }
    }
}