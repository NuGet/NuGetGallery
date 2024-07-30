// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using Moq;

namespace Validation.PackageSigning.Core.Tests.Support
{
    public static class DbSetMockFactory
    {
        public static Mock<IDbSet<TEntity>> CreateMock<TEntity>(params TEntity[] sourceList) where TEntity : class
        {
            return new Mock<IDbSet<TEntity>>().SetupDbSet(sourceList);
        }

        public static IDbSet<T> Create<T>(params T[] sourceList) where T : class
        {
            return CreateMock(sourceList).Object;
        }
    }
}