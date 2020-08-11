// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class OptimisticConcurrencyControlTokenTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("a")]
        public void WhenTokensAreEqual_EqualityTestsSucceed(string innerToken)
        {
            var token1 = new OptimisticConcurrencyControlToken(innerToken);
            var token2 = new OptimisticConcurrencyControlToken(innerToken);

            Assert.True(token1.Equals(token2));
            Assert.True(token1 == token2);
            Assert.False(token1 != token2);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "a")]
        [InlineData("a", "b")]
        public void WhenTokensAreNotEqual_EqualityTestsFail(string innerToken1, string innerToken2)
        {
            var token1 = new OptimisticConcurrencyControlToken(innerToken1);
            var token2 = new OptimisticConcurrencyControlToken(innerToken2);

            Assert.False(token1.Equals(token2));
            Assert.False(token1 == token2);
            Assert.True(token1 != token2);
        }

        [Fact]
        public void Null_EqualsNullToken()
        {
            var token1 = OptimisticConcurrencyControlToken.Null;
            var token2 = OptimisticConcurrencyControlToken.Null;

            Assert.True(token1.Equals(token2));
            Assert.True(token1 == token2);
            Assert.False(token1 != token2);
        }

        [Fact]
        public void GetHashCode_EqualsInnerTokenGetHashCode()
        {
            var innerToken = "abc";
            var token = new OptimisticConcurrencyControlToken(innerToken);

            Assert.Equal(innerToken.GetHashCode(), token.GetHashCode());
        }
    }
}