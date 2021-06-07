// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public static class DbMockHelpers
    {
        static internal DbSet<T> ListToDbSet<T>(List<T> entityList) where T: class, IEntity
        {
            var entityQueryable = entityList.AsQueryable();
            var dbSet = new Mock<DbSet<T>>();

            // boilerplate mock DbSet redirects:
            dbSet.As<IQueryable>().Setup(x => x.Provider).Returns(entityQueryable.Provider);
            dbSet.As<IQueryable>().Setup(x => x.Expression).Returns(entityQueryable.Expression);
            dbSet.As<IQueryable>().Setup(x => x.ElementType).Returns(entityQueryable.ElementType);
            dbSet.As<IQueryable>().Setup(x => x.GetEnumerator()).Returns(entityQueryable.GetEnumerator());

            // bypass any includes (which can break tests)
            dbSet.Setup(x => x.Include(It.IsAny<string>())).Returns(dbSet.Object);

            return dbSet.Object;
        }
    }
}
