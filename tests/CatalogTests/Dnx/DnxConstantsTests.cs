// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Dnx;
using Xunit;

namespace CatalogTests.Dnx
{
    public class DnxConstantsTests
    {
        [Fact]
        public void EnsureFrontCursorHasEnoughUpdates()
        {
            var totalTimeSpan = TimeSpan.FromSeconds(0);
            for (int i = 0; i < DnxConstants.MaxNumberOfUpdatesToKeepOfFrontCursor - 1; i++)
            {
                totalTimeSpan += DnxConstants.MinIntervalBetweenTwoUpdatesOfFrontCursor;
            }

            Assert.True(totalTimeSpan > DnxConstants.CacheDurationOfPackageVersionIndex);
        }
    }
}
