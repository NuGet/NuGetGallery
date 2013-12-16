using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public class Clock
    {
        public static readonly Clock RealClock = new Clock();

        public virtual DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }

        protected Clock() { }

        public virtual Task Delay(TimeSpan period)
        {
            return Task.Delay(period);
        }

        public virtual Task Delay(TimeSpan period, CancellationToken cancelToken)
        {
            return Task.Delay(period, cancelToken);
        }
    }

    public class VirtualClock : Clock
    {
        private readonly object _clockLock = new object();
        private DateTimeOffset _utcNow;
        private List<Tuple<DateTimeOffset, TaskCompletionSource<object>, CancellationToken>> _waits = new List<Tuple<DateTimeOffset, TaskCompletionSource<object>, CancellationToken>>();

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

        public override Task Delay(TimeSpan period)
        {
            return Delay(period, CancellationToken.None);
        }

        public override Task Delay(TimeSpan period, CancellationToken cancelToken)
        {
            lock (_clockLock)
            {
                var tcs = new TaskCompletionSource<object>();
                cancelToken.Register(() => tcs.TrySetCanceled());
                _waits.Add(Tuple.Create(_utcNow + period, tcs, cancelToken));
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
                if (wait.Item3.IsCancellationRequested)
                {
                    wait.Item2.TrySetCanceled();
                }
                else
                {
                    _waits.Remove(wait);
                    wait.Item2.TrySetResult(null);
                }
            }
        }
    }
}
