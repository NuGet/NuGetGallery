// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Extensions
{
    public class IEnumerableExtensionsFacts
    {
        [Theory]
        [MemberData(nameof(TheBatchMethodData))]
        public void TheBatchMethod(int[] input, int batchSize, List<List<int>> expected)
        {
            var actual = input.Batch(batchSize);

            AssertEqualBatches(expected, actual);
        }

        public static IEnumerable<object[]> TheBatchMethodData()
        {
            yield return new object[]
            {
                new[] { 1, 2, 3, },
                3,
                new List<List<int>>
                {
                    new List<int> { 1, 2, 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                2,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 }
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                1,
                new List<List<int>>
                {
                    new List<int> { 1 },
                    new List<int> { 2 },
                    new List<int> { 3 }
                }
            };
        }

        [Theory]
        [MemberData(nameof(TheWeightedBatchMethodData))]
        public void TheWeightedBatchMethod(int[] input, Func<int, int> weightFunc, int batchSize, List<List<int>> expected)
        {
            // Use each element's value as its weight
            var actual = input.WeightedBatch(batchSize, weightFunc);

            AssertEqualBatches(expected, actual);
        }

        public static IEnumerable<object[]> TheWeightedBatchMethodData()
        {
            Func<int, int> UseElementValueAsWeight = (int value) => value;

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                UseElementValueAsWeight,
                6,
                new List<List<int>>
                {
                    new List<int> { 1, 2, 3 },
                },
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                UseElementValueAsWeight,
                5,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                UseElementValueAsWeight,
                3,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                UseElementValueAsWeight,
                2,
                new List<List<int>>
                {
                    new List<int> { 1 },
                    new List<int> { 2 },
                    new List<int> { 3 }
                }
            };

            Func<int, int> AlwaysReturnWeightOf2 = (int value) => 2;

            yield return new object[]
            {
                new[] { 1 },
                AlwaysReturnWeightOf2,
                1,
                new List<List<int>>
                {
                    new List<int> { 1 }
                },
            };
        }

        private void AssertEqualBatches(List<List<int>> expected, List<List<int>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }
}
