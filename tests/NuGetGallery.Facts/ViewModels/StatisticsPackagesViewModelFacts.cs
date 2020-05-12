// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.ViewModels
{
    public class StatisticsPackagesViewModelFacts
    {
        [Theory]
        [InlineData(1004856283, 3, "1.00B")]   // pad with trailing zeroes test
        [InlineData(1403856283, 3, "1.40B")]   // pad with trailing zeroes test
        [InlineData(1406856283, 3, "1.41B")]
        [InlineData(1774856283, 3, "1.77B")]
        [InlineData(1775856283, 3, "1.78B")]
        [InlineData(1775856283, 5, "1.7759B")]
        [InlineData(775856283, 3, "776M")]
        [InlineData(775856283, 5, "775.86M")]
        [InlineData(75856283, 3, "75.9M")]
        [InlineData(56283, 3, "56.3k")]
        [InlineData(56283283283283, 3, "56.3T")]
        [InlineData(56283283283283283, 3, "56.3q")]
        [InlineData(56283283283283283283283283283283d, 3, "56.3n")]
        [InlineData(56283283283283283283283283283283283d, 3, "56.3 10^33")]
        [InlineData(1, 3, "1.00")]
        [InlineData(10, 3, "10.0")]
        [InlineData(100, 3, "100")]
        public void CreatesShortNumberRespectingSignificantFigures(double number, int sigFigs, string expected)
        {
            var result = StatisticsPackagesViewModel.DisplayShortNumber(number, sigFigs);
            Assert.Equal(expected, result);
        }
    }
}
