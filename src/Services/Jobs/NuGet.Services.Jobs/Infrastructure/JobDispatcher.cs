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

        public IReadOnlyList<JobDescription> Jobs { get { return _jobs.AsReadOnly(); } }
        
        public JobDispatcher(IEnumerable<JobDescription> jobs)
        {
            _jobs = jobs.ToList();
            _jobMap = _jobs.ToDictionary(j => j.Name, StringComparer.OrdinalIgnoreCase);
        }

        public virtual async Task<InvocationResult> Dispatch(InvocationContext context)
        {
            JobDescription jobDesc;
            if (!_jobMap.TryGetValue(context.Invocation.Job, out jobDesc))
            {
                throw new UnknownJobException(context.Invocation.Job);
            }
            JobBase job = jobDesc.CreateInstance();

            if (context.LogCapture != null)
            {
                context.LogCapture.SetJob(jobDesc, job);
            }

            InvocationEventSource.Log.Invoking(jobDesc);
            InvocationResult result = null;

            try
            {
                if (context.Invocation.Continuation)
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
                result = InvocationResult.Faulted(ex);
            }

            return result;
        }
    }
}
