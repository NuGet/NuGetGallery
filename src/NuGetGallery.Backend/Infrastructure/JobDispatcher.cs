using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Backend.Monitoring;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend
{
    public class JobDispatcher
    {
        private Dictionary<string, Job> _jobMap;
        private List<Job> _jobs;
        private BackendMonitoringHub _monitor;
        
        public IReadOnlyList<Job> Jobs { get { return _jobs.AsReadOnly(); } }
        public BackendConfiguration Config { get; private set; }

        public JobDispatcher(BackendConfiguration config, IEnumerable<Job> jobs, BackendMonitoringHub monitor)
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

        public virtual async Task<JobResponse> Dispatch(JobInvocation invocation, InvocationEventSource log, InvocationMonitoringContext monitoring)
        {
            Job job;
            if (!_jobMap.TryGetValue(invocation.Request.Name, out job))
            {
                throw new UnknownJobException(invocation.Request.Name);
            }

            if (monitoring != null)
            {
                await monitoring.SetJob(job);
            }

            log.Invoking(job);
            JobResult result = null;
            var context = new JobInvocationContext(invocation, Config, _monitor, log);
            try
            {
                result = await job.Invoke(context);
            }
            catch (Exception ex)
            {
                result = JobResult.Faulted(ex);
            }

            return new JobResponse(invocation, result, DateTimeOffset.UtcNow);
        }
    }
}
