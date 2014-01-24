using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Work
{
    /// <summary>
    /// Represents a single worker thread on a WorkService instance
    /// </summary>
    public class Worker
    {
        private Task _task;

        public int Id { get; private set; }
        public JobRunner Runner { get; private set; }
        public Worker(int id, JobRunner runner)
        {
            Id = id;
            Runner = runner;
        }

        public Task StartAndRun(CancellationToken cancelToken)
        {
            var task = Task.Factory.StartNew(
                function: () => Runner.Run(cancelToken),
                cancellationToken: cancelToken,
                creationOptions: TaskCreationOptions.LongRunning,
                scheduler: TaskScheduler.Current);
            _task = task.Unwrap();
            return _task;
        }

        public Task<object> GetCurrentStatus()
        {
            return Runner.GetCurrentStatus();
        }
    }
}
