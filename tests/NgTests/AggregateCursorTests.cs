// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace NgTests
{
    public class AggregateCursorTests
    {
        [Fact]
        public void ThrowsIfNoCursors()
        {
            Assert.Throws<ArgumentException>(() => new AggregateCursor(null));
            Assert.Throws<ArgumentException>(() => new AggregateCursor(new List<ReadCursor>()));
        }

        public static IEnumerable<object[]> ReturnsLeastValue_data
        {
            get
            {
                // Single
                yield return new object[]
                {
                    new List<DateTime>
                    {
                        new DateTime(2017, 4, 13)
                    }
                };

                // Two with least first
                yield return new object[]
                {
                    new List<DateTime>
                    {
                        new DateTime(2017, 4, 13),
                        new DateTime(2017, 4, 14)
                    }
                };

                // Two with least last
                yield return new object[]
                {
                    new List<DateTime>
                    {
                        new DateTime(2017, 4, 14),
                        new DateTime(2017, 4, 13)
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(ReturnsLeastValue_data))]
        public async Task ReturnsLeastValue(IEnumerable<DateTime> dates)
        {
            // Arrange
            var cursors = dates.Select(d => CreateReadCursor(d));
            var aggregateCursor = new AggregateCursor(cursors);

            // Act
            await aggregateCursor.LoadAsync(CancellationToken.None);
            var value = aggregateCursor.Value;

            // Assert
            Assert.Equal(dates.Min(), value);
        }

        private ReadCursor CreateReadCursor(DateTime date)
        {
            var cursor = new Mock<ReadCursor>();
            cursor.Setup(x => x.Value).Returns(() =>
            {
                cursor.Verify(x => x.LoadAsync(It.IsAny<CancellationToken>()));
                return date;
            });

            return cursor.Object;
        }
    }
}