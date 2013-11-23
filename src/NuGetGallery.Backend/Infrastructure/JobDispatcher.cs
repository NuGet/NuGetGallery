using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Backend.Monitoring;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend
{
    public class JobDispatcher
    {
        private Dictionary<string, JobBase> _jobMap;
        private List<JobBase> _jobs;
        private BackendMonitoringHub _monitor;
        
        public IReadOnlyList<JobBase> Jobs { get { return _jobs.AsReadOnly(); } }
        public BackendConfiguration Config { get; private set; }

        public JobDispatcher(BackendConfiguration config, IEnumerable<JobBase> jobs, BackendMonitoringHub monitor)
        {
            _jobs = jobs.ToList();
            _jobMap = _jobs.ToDictionary(j => j.Name);
            _monitor = monitor;
        
            Config = config;

            foreach (var job in _jobs)
            {
                WorkerEventSource.Log.JobDiscovered(job);
            }
        }

        public virtual async Task<JobResponse> Dispatch(JobInvocation invocation, InvocationMonitoringContext monitoring)
        {
            JobBase job;
            if (!_jobMap.TryGetValue(invocation.Request.Name, out job))
            {
                throw new UnknownJobException(invocation.Request.Name);
            }

            if (monitoring != null)
            {
                await monitoring.SetJob(job);
            }

            InvocationEventSource.Log.Invoking(job);
            JobResult result = null;
            var context = new JobInvocationContext(invocation, Config, _monitor);

            try
            {
                if (invocation.IsContinuation)
                {
                    IAsyncJob asyncJob = job as IAsyncJob;
                    if (asyncJob == null)
                    {
                        // Just going to be caught below, but that's what we want :).
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.JobDispatcher_AsyncContinuationOfNonAsyncJob,
                            job.Name));
                    }
                    result = await asyncJob.InvokeContinuation(context);
                }
                else
                {
                    result = await job.Invoke(context);
                }
            }
            catch (Exception ex)
            {
                result = JobResult.Faulted(ex);
            }

            return new JobResponse(invocation, result, DateTimeOffset.UtcNow);
        }
    }
}
