// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.ServiceBus.Tests
{
    public class BrokeredMessageWrapperFacts
    {
        [Fact]
        public void DoesNotMessUpScheduledEnqueueTimeUtc()
        {
            var now = DateTimeOffset.Now;

            var message = new BrokeredMessageWrapper("data");
            message.ScheduledEnqueueTimeUtc = now;
            Assert.Equal(now, message.ScheduledEnqueueTimeUtc);
        }

        [Fact]
        public void ForcesUtcOnScheduledEnqueueTimeUtc()
        {
            var now = DateTimeOffset.Now;
            var nowUtc = now.UtcDateTime;

            var message = new BrokeredMessageWrapper("data");
            message.ScheduledEnqueueTimeUtc = now;

            Assert.Equal(DateTimeKind.Utc, message.BrokeredMessage.ScheduledEnqueueTimeUtc.Kind);
            Assert.Equal(nowUtc, message.BrokeredMessage.ScheduledEnqueueTimeUtc);
            Assert.Equal(TimeSpan.Zero, message.ScheduledEnqueueTimeUtc.Offset);
        }

        [Fact]
        public void DefaultScheduledEnqueueTimeUtcIsNotInTheFuture()
        {
            var message = new BrokeredMessageWrapper("data");
            Assert.True(message.ScheduledEnqueueTimeUtc <= DateTimeOffset.Now);
        }

        [Fact]
        public void MinScheduledEnqueueTimeUtcWorks()
        {
            var message = new BrokeredMessageWrapper("data");
            message.ScheduledEnqueueTimeUtc = DateTimeOffset.MinValue;

            Assert.True(message.ScheduledEnqueueTimeUtc < DateTimeOffset.Now);
        }
    }
}
