// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Collector;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Collector
{
    public class CursorTests
    {
        public class TheGetMethod : CursorTest
        {
            [Fact]
            public async Task ThrowsIfNameNull()
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => Cursor.Get(null));
            }

            [Fact]
            public async Task ReturnsMinValueIfNotInTable()
            {
                Table
                    .Setup(x => x.RetrieveAsync<CursorEntity>(Name))
                    .ReturnsAsync((CursorEntity)null);

                var result = await Cursor.Get(Name);

                Assert.Equal(DateTime.MinValue, result);
            }

            [Fact]
            public async Task ReturnsValueIfInTable()
            {
                var entity = new CursorEntity(Name, new DateTime(2018, 9, 11));

                Table
                    .Setup(x => x.RetrieveAsync<CursorEntity>(Name))
                    .ReturnsAsync(entity);

                var result = await Cursor.Get(Name);

                Assert.Equal(entity.Value, result);
            }
        }

        public class TheSetMethod : CursorTest
        {
            [Fact]
            public async Task ThrowsIfNameNull()
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => Cursor.Set(null, new DateTime(2018, 9, 11)));
            }

            [Fact]
            public async Task InsertsOrReplacesExistingValueInTable()
            {
                var value = new DateTime(2018, 9, 11);

                Table
                    .Setup(x => x.InsertOrReplaceAsync(
                        It.Is<CursorEntity>(e => e.Name == Name && e.Value == value)))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Cursor.Set(Name, value);

                Table.Verify();
            }
        }

        public class CursorTest
        {
            public string Name => "name";
            public Mock<ITableWrapper> Table { get; }
            public Cursor Cursor { get; }

            public CursorTest()
            {
                Table = new Mock<ITableWrapper>();

                Cursor = new Cursor(
                    Table.Object,
                    Mock.Of<ILogger<Cursor>>());
            }
        }
    }
}