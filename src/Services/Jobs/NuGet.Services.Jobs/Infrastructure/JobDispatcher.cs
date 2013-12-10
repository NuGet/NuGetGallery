using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac.Features.OwnedInstances;
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
{
    public class JobDispatcher
    {
        private Dictionary<string, JobDescription> _jobMap;
        private List<JobDescription> _jobs;
        private JobsService _service;

        public IReadOnlyList<JobDescription> Jobs { get { return _jobs.AsReadOnly(); } }
        
        public JobDispatcher(JobsService service)
        {
            _jobs = service.Jobs.ToList();
            _jobMap = service.Jobs.ToDictionary(j => j.Name, StringComparer.OrdinalIgnoreCase);
        }

        public virtual async Task<InvocationResult> Dispatch(InvocationContext context)
        {
            JobDescription jobDesc;
            if (!_jobMap.TryGetValue(context.Invocation.Job, out jobDesc))
            {
                throw new UnknownJobException(context.Invocation.Job);
            }
            JobBase job = _service.Container.GetService<JobBase>(jobDesc.Type);

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
