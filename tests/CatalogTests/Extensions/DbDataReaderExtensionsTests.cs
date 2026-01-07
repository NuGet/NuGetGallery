// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using Moq;
using Xunit;

namespace CatalogTests.Extensions
{
    public class DbDataReaderExtensionsTests
    {
        public class TheReadNullableInt32Method
            : TheReadColumnOrNullMethodTestContainer<int?>
        {
            public TheReadNullableInt32Method()
                : base((r, c) => r.ReadInt32OrNull(c))
            {
            }

            public override void ReturnsValueWhenColumnHasValue()
            {
                const int ordinal = 1;
                const int exampleValue = 5;
                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);
                dataReaderMock.Setup(m => m.GetOrdinal(ColumnName)).Returns(ordinal);
                dataReaderMock.Setup(m => m.IsDBNull(ordinal)).Returns(false);
                dataReaderMock.Setup(m => m.GetInt32(ordinal)).Returns(exampleValue);

                var actual = ActualMethodBeingTested(dataReaderMock.Object, ColumnName);

                Assert.Equal(exampleValue, actual);
            }
        }

        public class TheReadNullableStringMethod
            : TheReadColumnOrNullMethodTestContainer<string>
        {
            public TheReadNullableStringMethod()
                : base((r, c) => r.ReadStringOrNull(c))
            {
            }

            public override void ReturnsValueWhenColumnHasValue()
            {
                const int ordinal = 1;
                const string exampleValue = "test";
                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);
                dataReaderMock.Setup(m => m.GetOrdinal(ColumnName)).Returns(ordinal);
                dataReaderMock.Setup(m => m.IsDBNull(ordinal)).Returns(false);
                dataReaderMock.Setup(m => m.GetString(ordinal)).Returns(exampleValue);

                var actual = ActualMethodBeingTested(dataReaderMock.Object, ColumnName);

                Assert.Equal(exampleValue, actual);
            }
        }

        public abstract class TheReadColumnOrNullMethodTestContainer<T>
        {
            protected const string ColumnName = "ColumnName";

            protected readonly Func<DbDataReader, string, T> ActualMethodBeingTested;

            public TheReadColumnOrNullMethodTestContainer(Func<DbDataReader, string, T> actualMethodBeingTested)
            {
                ActualMethodBeingTested = actualMethodBeingTested ?? throw new ArgumentNullException(nameof(actualMethodBeingTested));
            }

            [Fact]
            public void ReturnsNullWhenColumnDoesNotExist()
            {
                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);
                dataReaderMock.Setup(m => m.GetOrdinal(ColumnName)).Throws<IndexOutOfRangeException>();

                var actual = ActualMethodBeingTested(dataReaderMock.Object, ColumnName);

                Assert.Null(actual);
            }

            [Fact]
            public abstract void ReturnsValueWhenColumnHasValue();

            [Fact]
            public void ReturnsNullWhenColumnHasNoValue()
            {
                const int ordinal = 1;
                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);
                dataReaderMock.Setup(m => m.GetOrdinal(ColumnName)).Returns(ordinal);
                dataReaderMock.Setup(m => m.IsDBNull(ordinal)).Returns(true);

                var actual = ActualMethodBeingTested(dataReaderMock.Object, ColumnName);

                Assert.Null(actual);
            }
        }
    }
}