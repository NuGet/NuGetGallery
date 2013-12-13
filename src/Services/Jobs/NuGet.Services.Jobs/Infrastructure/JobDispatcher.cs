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
        private Dictionary<string, JobDefinition> _jobMap;
        private List<JobDefinition> _jobs;
        private IServiceProvider _container;

        public IReadOnlyList<JobDefinition> Jobs { get { return _jobs.AsReadOnly(); } }

        public JobDispatcher(IEnumerable<JobDefinition> jobs, IServiceProvider container)
        {
            _jobs = jobs.ToList();
            _jobMap = jobs.ToDictionary(j => j.Description.Name, StringComparer.OrdinalIgnoreCase);
            _container = container;
        }

        public virtual async Task<InvocationResult> Dispatch(InvocationContext context)
        {
            JobDefinition jobdef;
            if (!_jobMap.TryGetValue(context.Invocation.Job, out jobdef))
            {
                throw new UnknownJobException(context.Invocation.Job);
            }
            JobBase job = _container.GetService<JobBase>(jobdef.Implementation);

            if (context.LogCapture != null)
            {
                context.LogCapture.SetJob(jobdef, job);
            }

            Func<Task<InvocationResult>> invocationThunk = () => job.Invoke(context);
            if (context.Invocation.Continuation)
            {
                IAsyncJob asyncJob = job as IAsyncJob;
                if (asyncJob == null)
                {
                    // Just going to be caught below, but that's what we want :).
                    throw new InvalidOperationException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.JobDispatcher_AsyncContinuationOfNonAsyncJob,
                        jobdef.Description.Name));
                }
                invocationThunk = () => asyncJob.InvokeContinuation(context);
            }

            InvocationEventSource.Log.Invoking(jobdef);
            InvocationResult result = null;

            try
            {
                result = await invocationThunk();
            }
            catch (Exception ex)
            {
                result = InvocationResult.Faulted(ex);
            }

            return result;
        }
    }
}
