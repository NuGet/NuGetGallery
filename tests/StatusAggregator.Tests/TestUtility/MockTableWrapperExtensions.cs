// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Azure.Data.Tables;
using Moq;
using StatusAggregator.Table;

namespace StatusAggregator.Tests.TestUtility
{
    public static class MockTableWrapperExtensions
    {
        public static void SetupQuery<T>(
            this Mock<ITableWrapper> mock,
            params T[] results)
            where T : class, ITableEntity, new()
        {
            mock
                .Setup(x => x.CreateQuery<T>())
                .Returns(results.AsQueryable());
        }
    }
}
