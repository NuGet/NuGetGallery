// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Ng.Jobs;
using Xunit;

namespace NgTests.Jobs
{
    public class Catalog2DnxJobTests
    {
        [Fact]
        public void EnsureFrontCursorHasEnoughUpdates()
        {
            var totalTimeSpan = TimeSpan.FromSeconds(0);
            for (int i = 0; i < Catalog2DnxJob.MaxNumberOfUpdatesToKeepOfFrontCursor - 1; i++)
            {
                totalTimeSpan += Catalog2DnxJob.MinIntervalBetweenTwoUpdatesOfFrontCursor;
            }

            Assert.True(totalTimeSpan > Catalog2DnxJob.CacheDurationOfPackageVersionIndex);
        }
    }
}
