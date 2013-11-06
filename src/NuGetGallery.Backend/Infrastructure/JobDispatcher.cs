using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Backend.Tracing;

namespace NuGetGallery.Backend
{
    public class JobDispatcher
    {
        private Dictionary<string, Job> _jobMap;
        private List<Job> _jobs;

        public IReadOnlyList<Job> Jobs { get { return _jobs.AsReadOnly(); } }
        public BackendConfiguration Config { get; private set; }

        public JobDispatcher(BackendConfiguration config, IEnumerable<Job> jobs)
        {
            _jobs = jobs.ToList();
            _jobMap = _jobs.ToDictionary(j => j.Name);

            Config = config;

            foreach (var job in _jobs)
            {
                WorkerEventSource.Log.JobDiscovered(job);
            }
        }

        public virtual JobResponse Dispatch(JobRequest request)
        {
            Job job;
            if (!_jobMap.TryGetValue(request.Name, out job))
            {
                throw new UnknownJobException(request.Name);
            }

            var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow, "Dispatcher", Config);
            WorkerEventSource.Log.DispatchingRequest(invocation);
            var result = job.Invoke(invocation);

            return new JobResponse(invocation, result, DateTimeOffset.UtcNow);
        }
    }
}
