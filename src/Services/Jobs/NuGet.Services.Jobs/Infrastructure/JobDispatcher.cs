using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
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
            _jobMap = _jobs.ToDictionary(j => j.Name, StringComparer.OrdinalIgnoreCase);
            _monitor = monitor;
        
            Config = config;
        }

        public virtual async Task<JobResponse> Dispatch(JobInvocationContext context)
        {
            JobDescription jobDesc;
            if (!_jobMap.TryGetValue(context.Invocation.Request.Name, out jobDesc))
            {
                throw new UnknownJobException(context.Invocation.Request.Name);
            }
            JobBase job = jobDesc.CreateInstance();

            if (context.Monitoring != null)
            {
                await context.Monitoring.SetJob(jobDesc, job);
            }

            InvocationEventSource.Log.Invoking(jobDesc);
            JobResult result = null;

            try
            {
                if (context.Invocation.IsContinuation)
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

            return new JobResponse(context.Invocation, result, DateTimeOffset.UtcNow);
        }
    }
}
