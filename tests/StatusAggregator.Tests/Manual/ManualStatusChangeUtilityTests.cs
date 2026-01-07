// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using StatusAggregator.Manual;
using System;
using Xunit;

namespace StatusAggregator.Tests.Manual
{
    public class ManualStatusChangeUtilityTests
    {
        public abstract class BaseMethod
        {
            protected abstract bool GetResult(EventEntity eventEntity, bool eventIsActive, DateTime timestamp);

            protected abstract void AssertChangedTo(EventEntity eventEntity, DateTime? initialEndTime, DateTime? timestamp);

            [Fact]
            public void ThrowsIfEventIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => GetResult(null, false, DateTime.MinValue));
            }

            [Fact]
            public void ActiveEventStaysActive()
            {
                var eventEntity = new EventEntity("", new DateTime(2018, 8, 17));

                var result = GetResult(eventEntity, true, new DateTime(2018, 8, 18));

                Assert.False(result);
                Assert.Null(eventEntity.EndTime);
            }

            [Fact]
            public void ActiveEventBecomesInactive()
            {
                DateTime? initialEndTime = null;
                var deactivatedTime = new DateTime(2018, 8, 18);
                var eventEntity = new EventEntity("", new DateTime(2018, 8, 17), endTime: initialEndTime);

                var result = GetResult(eventEntity, false, deactivatedTime);

                Assert.True(result);
                AssertChangedTo(eventEntity, initialEndTime, deactivatedTime);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DeactivatedEventIsUnaffected(bool eventIsActive)
            {
                var deactivatedTime = new DateTime(2018, 8, 18);
                var eventEntity = new EventEntity("", new DateTime(2018, 8, 17), endTime: deactivatedTime);

                var result = GetResult(eventEntity, eventIsActive, new DateTime(2018, 8, 19));

                Assert.False(result);
                Assert.Equal(deactivatedTime, eventEntity.EndTime);
            }
        }

        public class TheUpdateEventIsActiveMethod : BaseMethod
        {
            protected override bool GetResult(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
            {
                return ManualStatusChangeUtility.UpdateEventIsActive(eventEntity, eventIsActive, timestamp);
            }

            protected override void AssertChangedTo(EventEntity eventEntity, DateTime? initialEndTime, DateTime? timestamp)
            {
                Assert.Equal(timestamp, eventEntity.EndTime);
            }
        }

        public class TheShouldEventBeActiveMethod : BaseMethod
        {
            protected override bool GetResult(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
            {
                return ManualStatusChangeUtility.ShouldEventBeActive(eventEntity, eventIsActive, timestamp);
            }

            protected override void AssertChangedTo(EventEntity eventEntity, DateTime? initialEndTime, DateTime? timestamp)
            {
                Assert.Equal(initialEndTime, eventEntity.EndTime);
            }
        }
    }
}