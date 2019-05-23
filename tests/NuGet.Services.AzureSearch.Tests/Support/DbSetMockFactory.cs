// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Moq;

namespace NuGet.Services.AzureSearch.Support
{
    public static class DbSetMockFactory
    {
        public static DbSet<T> Create<T>(params T[] sourceList) where T : class
        {
            var list = new List<T>(sourceList);

            var mockSet = new Mock<DbSet<T>>();
            mockSet.As<IDbAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator())
                .Returns(() => new TestDbAsyncEnumerator<T>(list.AsQueryable().GetEnumerator()));

            mockSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(() => new TestDbAsyncQueryProvider<T>(list.AsQueryable().Provider));

            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(() => list.AsQueryable().Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(() => list.AsQueryable().ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => list.AsQueryable().GetEnumerator());

            mockSet.Setup(m => m.Include(It.IsAny<string>())).Returns(() => mockSet.Object);
            mockSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(e => list.Add(e));
            mockSet.Setup(m => m.Remove(It.IsAny<T>())).Callback<T>(e => list.Remove(e));

            return mockSet.Object;
        }
    }
}
