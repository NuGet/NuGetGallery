// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class CurrentTimestampFacts
    {
        [Fact]
        public void CapturesCurrentTimestampOnNextRead()
        {
            var target = new CurrentTimestamp();

            target.SetOnNextRead();
            var before = DateTimeOffset.UtcNow;
            var actual = target.Value;
            var after = DateTimeOffset.UtcNow;

            Assert.NotNull(actual);
            Assert.InRange(actual.Value, before, after);
        }

        [Fact]
        public void RetainsValueAfterFirstRead()
        {
            var target = new CurrentTimestamp();
            target.SetOnNextRead();
            var initial = target.Value;

            var actual = target.Value;

            Assert.NotNull(actual);
            Assert.Equal(initial, actual);
        }

        [Fact]
        public void ReplacesSetValueWhenFlagIsSet()
        {
            var target = new CurrentTimestamp();
            target.SetOnNextRead();
            target.Value = DateTimeOffset.MaxValue;

            var actual = target.Value;

            Assert.NotNull(actual);
            Assert.NotEqual(DateTimeOffset.MaxValue, actual);
        }

        [Fact]
        public void DoesNotReplaceSetValueWhenFlagIsNotSet()
        {
            var target = new CurrentTimestamp();
            target.Value = DateTimeOffset.MaxValue;

            var actual = target.Value;

            Assert.Equal(DateTimeOffset.MaxValue, actual);
        }
    }
}
