// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.AzureCdnLogs.Common
{
    public class AzureBlobLeaseManagerTests
    {
        [Fact]
        public void ConstructorNullArgumentTest()
        {
            Assert.Throws<ArgumentNullException>(() => new AzureBlobLeaseManager(null, null));
        }

        [Fact]
        public void MaxRenewPeriodInSecondsTest()
        {
            Assert.True(AzureBlobLeaseManager.MaxRenewPeriodInSeconds > AzureBlobLeaseManager.OverlapRenewPeriodInSeconds);
        }
    }
}
