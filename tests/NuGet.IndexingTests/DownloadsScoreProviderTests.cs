// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class DownloadsScoreProviderTests
    {
        [Theory]
        [InlineData(int.MinValue, 1)]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(10, 1)]
        [InlineData(999, 1)]
        [InlineData(1000, 1)]
        [InlineData(1001, 1)]
        [InlineData(1300, 1.12)]
        [InlineData(13000, 1.20)]
        [InlineData(130000, 1.26)]
        [InlineData(1300000, 1.31)]
        [InlineData(13000000, 1.36)]
        [InlineData(1000000000, 1.45)] // 1B
        [InlineData(int.MaxValue, 1.47)]
        public void ValidateDownloadsFormula(int downloads, double expected)
        {
            // Validate the curve of the boost
            double factor = DownloadsScoreProvider.DownloadScore(downloads);

            factor = Math.Round(factor, 2);

            Assert.Equal(expected, factor);
        }
    }
}
