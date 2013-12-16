using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public static class Clock
    {
        private static ClockManager _manager;

        public static Clock Current { get { return _manager.CurrentClock; } }
        public static DateTimeOffset UtcNow { get { return Current.UtcNow; } }

        public static Task Delay(TimeSpan period)
        {
            return Current.Delay(period);
        }

        internal class ClockManager 
        {
            public virtual Clock CurrentClock { get; set; }

            public ClockManager()
            {
                CurrentClock = RealClock.Instance;
            }
        }

        // TODO: When VirtualClock is enabled, replace the ClockManager with a CallContextClockManager
    }

    public class RealClock
    {
        public static readonly RealClock Instance = new RealClock();

        public virtual DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }

        protected RealClock() { }

        public virtual Task Delay(TimeSpan period)
        {
            return Task.Delay(period)
        }

        public virtual Task Delay(TimeSpan period, CancellationToken cancelToken)
        {
            return Task.Delay(period, cancelToken);
        }
    }

    public class VirtualClock : RealClock
    {
        private readonly object _clockLock = new object();
        private DateTimeOffset _utcNow;
        private List<Tuple<DateTimeOffset, TaskCompletionSource<object>>> _waits = new List<Tuple<DateTimeOffset, TaskCompletionSource<object>>>();

        public override DateTimeOffset UtcNow
        {
            get
            {
                lock (_clockLock)
                {
                    return _utcNow;
                }
            }
        }

        public VirtualClock() : this(DateTimeOffset.UtcNow) { }
        public VirtualClock(DateTimeOffset startTime)
        {
            _utcNow = startTime;
        }

        public virtual Task Delay(TimeSpan period)
        {
            return Delay(period, CancellationToken.None);
        }

        public virtual Task Delay(TimeSpan period, CancellationToken cancelToken)
        {
            lock (_clockLock)
            {
                var tcs = new TaskCompletionSource<object>();
                cancelToken.Register(() => tcs.TrySetCanceled());
                _waits.Add(Tuple.Create(_utcNow + period, tcs));
                return tcs.Task;
            }
        }

        public void Advance(TimeSpan period)
        {
            lock (_clockLock)
            {
                Set(_utcNow + period);
            }
        }

        public void Set(DateTimeOffset utcNow)
        {
            lock (_clockLock)
            {
                _utcNow = utcNow;
                TimeChangedUnlocked();
            }
        }

        private void TimeChangedUnlocked()
        {
            var fired = _waits.Where(t => t.Item1 <= _utcNow).ToList();
            foreach (var wait in fired)
            {
                _waits.Remove(wait);
                wait.Item2.TrySetResult(null);
            }
        }
    }
}
