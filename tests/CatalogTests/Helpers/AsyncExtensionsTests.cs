// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NuGetAsyncExtensions = NuGet.Services.Metadata.Catalog.Helpers.AsyncExtensions;

namespace CatalogTests.Helpers
{
    public class AsyncExtensionsTests
    {
        [Fact]
        public async Task ForEachAsync_WhenEnumerableIsNull_Throws()
        {
            IEnumerable<string> enumerable = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => NuGetAsyncExtensions.ForEachAsync(enumerable, maxDegreeOfParallelism: 2, func: _ => Task.FromResult(0)));

            Assert.Equal("enumerable", exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task ForEachAsync_WhenMaxDegreeOfParallelismIsLessThanOne_Throws(int maxDegreeOfParallelism)
        {
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => NuGetAsyncExtensions.ForEachAsync(Enumerable.Empty<string>(), maxDegreeOfParallelism, func: _ => Task.FromResult(0)));

            Assert.Equal("maxDegreeOfParallelism", exception.ParamName);
            Assert.StartsWith($"The argument must be within the range from 1 (inclusive) to {int.MaxValue} (inclusive).", exception.Message);
        }

        [Fact]
        public async Task ForEachAsync_WhenFuncIsNull_Throws()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => NuGetAsyncExtensions.ForEachAsync(Enumerable.Empty<string>(), maxDegreeOfParallelism: 2, func: null));

            Assert.Equal("func", exception.ParamName);
        }

        [Fact]
        public async Task ForEachAsync_WithValidArguments_ProcessesAllItems()
        {
            var enumerable = Enumerable.Range(1, 100);
            var bag = new ConcurrentBag<int>();

            await NuGetAsyncExtensions.ForEachAsync(
                enumerable,
                maxDegreeOfParallelism: 10,
                func: i =>
                {
                    bag.Add(i);

                    return Task.FromResult(0);
                });

            var actualResults = bag.ToArray();

            Array.Sort(actualResults);

            Assert.Equal(enumerable, actualResults);
        }
    }
}