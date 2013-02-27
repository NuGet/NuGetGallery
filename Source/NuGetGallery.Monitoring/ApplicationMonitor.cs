using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public abstract class ApplicationMonitor
    {
        protected IEventReporter Reporter { get; private set; }
        protected CancellationToken CancelToken { get; private set; }

        protected virtual string DefaultResourceName { get { return null; } }
        
        public virtual async Task Invoke(IEventReporter reporter, CancellationToken cancelToken)
        {
            var oldrep = Reporter; // Just in case, let's capture the old reporter
            Reporter = reporter;
            CancelToken = cancelToken;
            await Invoke();
            Reporter = oldrep;
            CancelToken = CancellationToken.None;
        }

        protected abstract Task Invoke();

        protected virtual void Success(string message, string resource = null)
        {
            Reporter.Report(new MonitoringMessageEvent(
                EventType.Success,
                DateTime.UtcNow,
                message, 
                resource ?? DefaultResourceName));
        }

        protected virtual void MonitorFailure(string message, string resource = null)
        {
            Reporter.Report(new MonitoringMessageEvent(
                EventType.MonitorFailure,
                DateTime.UtcNow,
                message,
                resource ?? DefaultResourceName));
        }

        protected virtual void Failure(string message, string resource = null)
        {
            Reporter.Report(new MonitoringMessageEvent(
                EventType.Failure,
                DateTime.UtcNow,
                message,
                resource ?? DefaultResourceName));
        }

        protected virtual void Degraded(string message, string resource = null)
        {
            Reporter.Report(new MonitoringMessageEvent(
                EventType.Degraded,
                DateTime.UtcNow,
                message,
                resource ?? DefaultResourceName));
        }

        protected virtual void Unhealthy(string message, string resource = null)
        {
            Reporter.Report(new MonitoringMessageEvent(
                EventType.Unhealthy,
                DateTime.UtcNow,
                message,
                resource ?? DefaultResourceName));
        }

        protected virtual void QoS(string action, bool success, TimeSpan timeTaken, string resource = null)
        {
            Reporter.Report(new MonitoringQoSEvent(
                success,
                timeTaken,
                DateTime.UtcNow,
                action,
                resource ?? DefaultResourceName));
        }

        protected virtual void QoS(string action, bool success, int value, string resource = null)
        {
            Reporter.Report(new MonitoringQoSEvent(
                success,
                value,
                DateTime.UtcNow,
                action,
                resource ?? DefaultResourceName));
        }

        private static readonly object Empty = new object();
        protected async Task<TimeResult> Time(Func<Task> act)
        {
            // Make the result a true "TimeResult" (strip out the <T> parts)
            var result = await Time(async () => { await act(); return Empty; });
            return new TimeResult(result.Time, result.Exception);
        }

        protected async Task<TimeResult<T>> Time<T>(Func<Task<T>> func)
        {
            Stopwatch sw = new Stopwatch();
            T result = default(T);
            sw.Start();
            try
            {
                result = await func();
            }
            catch (Exception ex)
            {
                if (sw.IsRunning)
                {
                    sw.Stop();
                }
                return new TimeResult<T>(sw.Elapsed, ex, result);
            }
            if (sw.IsRunning)
            {
                sw.Stop();
            }
            return new TimeResult<T>(sw.Elapsed, null, result);
        }

        public class TimeResult
        {
            public TimeSpan Time { get; private set; }
            public Exception Exception { get; private set; }
            public bool IsSuccess { get { return Exception == null; } }

            public TimeResult(TimeSpan time, Exception ex)
            {
                Time = time;
                Exception = ex;
            }
        }

        public class TimeResult<T> : TimeResult
        {
            public T Result { get; private set; }

            public TimeResult(TimeSpan time, Exception ex, T result) : base(time, ex)
            {
                Result = result;
            }
        }
    }
}
