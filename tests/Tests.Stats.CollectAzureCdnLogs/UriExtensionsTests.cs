// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs
{
    public class UriExtensionsTests
    {
        [Theory]
        [InlineData("ftp://someserver/logs")]
        [InlineData("ftp://someserver/logs/")]
        public void EnsureTrailingSlashOnlyAppendsWhenMissing(string uriString)
        {
            var uri = new Uri(uriString);
            var result = UriExtensions.EnsureTrailingSlash(uri);

            Assert.Equal("ftp://someserver/logs/", result.ToString());
        }
    }
}