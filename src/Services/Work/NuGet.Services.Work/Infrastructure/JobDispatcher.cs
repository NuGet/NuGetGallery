using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Work.Monitoring;

namespace NuGet.Services.Work
{
    public class JobDispatcher
    {
        private Dictionary<string, JobDescription> _jobMap;
        private List<JobDescription> _jobs;
        private ILifetimeScope _container;

        private JobDescription _currentJob = null;

        public IReadOnlyList<JobDescription> Jobs { get { return _jobs.AsReadOnly(); } }

        protected JobDispatcher() { }

        public JobDispatcher(IEnumerable<JobDescription> jobs, ILifetimeScope container)
            : this()
        {
            _jobs = jobs.ToList();
            _jobMap = jobs.ToDictionary(j => j.Name, StringComparer.OrdinalIgnoreCase);
            _container = container;
        }

        public virtual async Task<InvocationResult> Dispatch(InvocationContext context)
        {
            JobDescription jobdef;
            if (!_jobMap.TryGetValue(context.Invocation.Job, out jobdef))
            {
                throw new UnknownJobException(context.Invocation.Job);
            }
            Interlocked.Exchange(ref _currentJob, jobdef);

            ILifetimeScope scope = null;
            using (scope = _container.BeginLifetimeScope(b =>
            {
                b.RegisterType(jobdef.Implementation).As(jobdef.Implementation);
                b.RegisterInstance(context).As<InvocationContext>();
                b.Register(ctx => scope)
                    .As<ILifetimeScope>();
            }))
            {
                var job = (JobHandlerBase)scope.Resolve(jobdef.Implementation);

                Func<Task<InvocationResult>> invocationThunk = () => job.Invoke(context);
                if (context.Invocation.IsContinuation)
                {
                    IAsyncJob asyncJob = job as IAsyncJob;
                    if (asyncJob == null)
                    {
                        // Just going to be caught below, but that's what we want :).
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.JobDispatcher_AsyncContinuationOfNonAsyncJob,
                            jobdef.Name));
                    }
                    invocationThunk = () => asyncJob.InvokeContinuation(context);
                }

                InvocationEventSource.Log.Invoking(jobdef);
                InvocationResult result = null;

                try
                {
                    context.SetJob(jobdef, job);

                    result = await invocationThunk();
                }
                catch (Exception ex)
                {
                    result = InvocationResult.Faulted(ex);
                }
                Interlocked.Exchange(ref _currentJob, null);
                return result;
            }
        }

        public JobDescription GetCurrentJob()
        {
            return _currentJob;
        }
    }
}
