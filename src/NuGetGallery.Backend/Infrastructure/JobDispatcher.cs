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
        private Dictionary<string, JobDescription> _jobMap;
        private List<JobDescription> _jobs;
        private BackendMonitoringHub _monitor;

        public IReadOnlyList<JobDescription> Jobs { get { return _jobs.AsReadOnly(); } }
        public BackendConfiguration Config { get; private set; }

        public JobDispatcher(BackendConfiguration config, IEnumerable<JobDescription> jobs, BackendMonitoringHub monitor)
        {
            _jobs = jobs.ToList();
            _jobMap = _jobs.ToDictionary(j => j.Name);
            _monitor = monitor;
        
            Config = config;
        }

        public virtual async Task<JobResponse> Dispatch(JobInvocation invocation, InvocationMonitoringContext monitoring)
        {
            JobDescription jobDesc;
            if (!_jobMap.TryGetValue(invocation.Request.Name, out jobDesc))
            {
                throw new UnknownJobException(invocation.Request.Name);
            }
            JobBase job = jobDesc.CreateInstance();

            if (monitoring != null)
            {
                await monitoring.SetJob(jobDesc, job);
            }

            InvocationEventSource.Log.Invoking(jobDesc);
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
                            jobDesc.Name));
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
