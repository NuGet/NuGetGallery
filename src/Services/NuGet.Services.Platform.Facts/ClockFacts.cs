using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services
{
    public class ClockFacts
    {
        public class TheRealClock
        {
            [Fact]
            public void ReturnsRealTimeForUtcNow() 
            {
                var testStart = DateTimeOffset.UtcNow;
                Assert.True(Clock.RealClock.UtcNow >= testStart);
            }

            [Fact]
            public async Task DelaysForRealTime()
            {
                var captured = Clock.RealClock.UtcNow;
                await Clock.RealClock.Delay(TimeSpan.FromMilliseconds(10));
                Assert.True(Clock.RealClock.UtcNow >= captured);
            }
        }

        public class TheVirtualClock
        {
            [Fact]
            public void ReturnsCurrentTimeAsUtcNowIfNoStartTimeProvided()
            {
                var testStart = DateTimeOffset.UtcNow;
                var clock = new VirtualClock();
                Assert.True(clock.UtcNow >= testStart);
            }

            [Fact]
            public async Task DoesNotAdvanceWithRealTime()
            {
                // Arrange
                var testStart = DateTimeOffset.UtcNow;
                var clock = new VirtualClock(testStart);
                var captured = clock.UtcNow;

                // Act
                // Use real Task.Delay because we need to sleep for real time
                await Task.Delay(TimeSpan.FromMilliseconds(10));

                // Assert
                Assert.Equal(captured, clock.UtcNow);
                Assert.True(DateTimeOffset.UtcNow >= clock.UtcNow);
            }

            [Fact]
            public void AdvancesWithVirtualTime()
            {
                // Arrange
                var clock = new VirtualClock(new DateTimeOffset(2010, 01, 01, 01, 01, 01, TimeSpan.Zero));
                
                // Act
                clock.Advance(TimeSpan.FromDays(10));

                // Assert
                Assert.Equal(new DateTimeOffset(2010, 01, 11, 01, 01, 01, TimeSpan.Zero), clock.UtcNow);
            }

            [Fact]
            public void DelayWaitsForVirtualTimeToElapse()
            {
                // Arrange
                var clock = new VirtualClock(new DateTimeOffset(2010, 01, 01, 01, 01, 01, TimeSpan.Zero));
                var task = clock.Delay(TimeSpan.FromMilliseconds(1));

                // Assume - Task has not completed
                Assert.False(task.IsCompleted);

                // Act
                clock.Advance(TimeSpan.FromSeconds(1));
                
                // Assert
                Assert.True(task.IsCompleted);
            }
        }
    }
}
