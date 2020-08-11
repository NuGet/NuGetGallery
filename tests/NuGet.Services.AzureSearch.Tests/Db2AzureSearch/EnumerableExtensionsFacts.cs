// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class EnumerableExtensionsFacts
    {
        public class Batch
        {
            [Fact]
            public void PutsItemAboveMaxInOwnBatch()
            {
                var items = new[]
                {
                    "aaa",
                    "bb",
                    "c",
                    "ddddddddddd",
                    "eeee",
                };

                var batches = BatchAndJoin(items, 8);

                Assert.Equal(
                    new[]
                    {
                        "aaa|bb|c",
                        "ddddddddddd",
                        "eeee"
                    },
                    batches);
            }

            [Fact]
            public void AllowsBatchSizeToExactlyMatch()
            {
                var items = new[]
                {
                    "aaa",
                    "bb",
                    "c",
                    "ddddddddddd",
                    "ee",
                    "ffff",
                };

                var batches = BatchAndJoin(items, 6);

                Assert.Equal(
                    new[]
                    {
                        "aaa|bb|c",
                        "ddddddddddd",
                        "ee|ffff"
                    },
                    batches);
            }

            [Fact]
            public void HandlesZeroItems()
            {
                var items = new[]
                {
                    "aaa",
                    "",
                    "",
                    "b",
                    "ccccc",
                    "",
                    "",
                    "",
                    "d"
                };

                var batches = BatchAndJoin(items, 5);

                Assert.Equal(
                    new[]
                    {
                        "aaa|||b",
                        "ccccc|||",
                        "d"
                    },
                    batches);
            }

            [Fact]
            public void ReturnsEmptyForEmpty()
            {
                var items = new string[0];

                var batches = BatchAndJoin(items, 8);

                Assert.Empty(batches);
            }

            [Fact]
            public void AllowsSingleElementSmallerThanMax()
            {
                var items = new[] { "a" };

                var batches = BatchAndJoin(items, 8);

                Assert.Equal(new[] { "a" }, batches);
            }

            [Fact]
            public void AllowsSingleElementLargerThanMax()
            {
                var items = new[] { "aaa" };

                var batches = BatchAndJoin(items, 2);

                Assert.Equal(new[] { "aaa" }, batches);
            }

            private static string[] BatchAndJoin(string[] items, int maxSize)
            {
                return items
                    .Batch(x => x.Length, maxSize)
                    .Select(x => string.Join("|", x))
                    .ToArray();
            }
        }
    }
}
