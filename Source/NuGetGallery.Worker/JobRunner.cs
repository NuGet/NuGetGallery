using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using NuGetGallery.Worker.Jobs;

namespace NuGetGallery.Worker
{
    [Export]
    public class JobRunner
    {
        private AsyncSubject<Unit> _subject = new AsyncSubject<Unit>();
        private Logger _logger = LogManager.GetLogger("JobRunner");
        private Settings _settings;

        public IDictionary<string, WorkerJob> Jobs { get; private set; }

        [ImportingConstructor]
        public JobRunner([Import(AllowDefault=true)] Settings settings, [ImportMany] IEnumerable<WorkerJob> jobs)
        {
            _settings = settings ?? new Settings();
            Jobs = jobs.ToDictionary(j => j.GetType().Name, StringComparer.OrdinalIgnoreCase);
            _subject.OnNext(Unit.Default);

            foreach (var job in Jobs.Values)
            {
                job.Initialize(_settings);
            }
        }

        public void Stop()
        {
            _subject.OnCompleted();
            _logger.Info("Stopped Job Runner");
        }

        public void Run()
        {
            Run(Jobs);
        }

        void Run(IDictionary<string, WorkerJob> jobs)
        {
            _logger.Info("Scheduling Jobs...");

            JobReport.Initialize(_settings);

            IList<JobStatusReport> reports = new List<JobStatusReport>();
            foreach (string name in jobs.Keys)
            {
                WorkerJob workerJob = jobs[name];
                DateTime startTime = DateTime.UtcNow + workerJob.Offset;

                string message = string.Format("scheduled (every {0} from {1})", workerJob.Period, startTime.ToString("yyyy-MM-dd HH:mm:ss"));

                reports.Add(new JobStatusReport
                {
                    JobName = name,
                    At = DateTime.UtcNow.ToString(),
                    Duration = "0",
                    Status = "success",
                    Message = message
                });
            }
            JobReport.Update(_settings, reports.ToArray());

            // Set up the schedules
            var tokens = jobs.Select(job =>
            {
                var startTime = DateTimeOffset.UtcNow + job.Value.Offset;
                _logger.Debug("Scheduling '{0}' to run every '{1}' starting at '{2}'.", job.Value.GetType().Name, job.Value.Period, startTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                return Observable.Timer(startTime, job.Value.Period)
                                 .Subscribe(_ => RunJob(job.Key, job.Value));
            }).ToArray();

            // Wait for a completion message
            _logger.Info("Ready at {0}. Waiting for Shutdown", DateTime.Now);
            _subject.Wait();

            _logger.Info("Shutting down jobs...");
            foreach (var token in tokens)
            {
                token.Dispose();
            }
        }

        public void RunSingleJob(string name)
        {
            _logger.Info("Running " + name);

            WorkerJob job;
            if (!Jobs.TryGetValue(name, out job))
            {
                _logger.Error("No such job: " + name);
                throw new InvalidOperationException("No such job: " + name);
            }
            RunJob(name, job);
        }

        public void RunSingleJobContinuously(string name)
        {
            WorkerJob job;
            if (!Jobs.TryGetValue(name, out job))
            {
                _logger.Error("No such job: " + name);
                throw new InvalidOperationException("No such job: " + name);
            }

            Run(new Dictionary<string, WorkerJob> { { name, job } });
        }

        public void OnStop()
        {
            foreach (var job in Jobs)
            {
                _logger.Debug("Cleaning up Job '{0}'", job.Key);
            }
        }

        public bool OnStart()
        {
            foreach (var job in Jobs)
            {
                _logger.Debug("Initializing Job '{0}'", job.Key);
            }
            return true;
        }

        private void RunJob(string name, WorkerJob job)
        {
            _logger.Debug("Executing Job '{0}'", name);

            DateTime before = DateTime.UtcNow;

            try
            {
                job.RunOnce();

                DateTime after = DateTime.UtcNow;

                JobReport.Update(_settings, new JobStatusReport
                {
                    JobName = name,
                    At = before.ToString(),
                    Duration = (after - before).TotalSeconds.ToString("F2"),
                    Status = "success",
                    Message = job.StatusMessage,
                    Exception = null
                });
            }
            catch (Exception ex)
            {
                DateTime after = DateTime.UtcNow;
                
                JobReport.Update(_settings, new JobStatusReport
                {
                    JobName = name,
                    At = before.ToString(),
                    Duration = (after - before).TotalSeconds.ToString("F2"),
                    Status = "failure",
                    Message = ex.Message,
                    Exception = ex
                });

                _logger.ErrorException(String.Format("Error Executing Job '{0}', Exception: {1}", name, ex.Message), ex);
            }
        }
    }
}
