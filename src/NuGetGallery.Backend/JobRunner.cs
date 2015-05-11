// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using NuGetGallery.Backend.Jobs;

namespace NuGetGallery.Backend
{
    [Export]
    public class JobRunner : IDisposable
    {
        private AsyncSubject<Unit> _subject = new AsyncSubject<Unit>();
        private Logger _logger = LogManager.GetLogger("JobRunner");
        private Settings _settings;

        public IDictionary<string, WorkerJob> Jobs { get; private set; }

        [ImportingConstructor]
        public JobRunner([Import(AllowDefault = true)] Settings settings, [ImportMany] IEnumerable<WorkerJob> jobs)
        {
            _settings = settings ?? new Settings();
            Jobs = jobs.ToDictionary(j => j.GetType().Name, StringComparer.OrdinalIgnoreCase);
            _subject.OnNext(Unit.Default);

            foreach (var job in Jobs.Values)
            {
                try
                {
                    job.Initialize(_settings);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException(String.Format("{2} Initializing '{0}': {1}", job.GetType().Name, ex.Message, ex.GetType().Name), ex);
                }
            }
        }

        public void Dispose()
        {
            _subject.Dispose();
            GC.SuppressFinalize(this);
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

            // Set up the schedules
            IDisposable[] tokens;
            try
            {
                tokens = jobs.Select(job =>
                {
                    var startTime = DateTimeOffset.UtcNow + job.Value.Offset;
                    _logger.Debug("Scheduling '{0}' to run every '{1}' starting at '{2}'.", job.Value.GetType().Name, job.Value.Period, startTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    return Observable.Timer(startTime, job.Value.Period)
                                     .Subscribe(_ => RunJob(job.Key, job.Value));
                }).ToArray();
            }
            catch (Exception ex)
            {
                _logger.ErrorException(String.Format("Error scheduling jobs: {0}", ex.Message), ex);
                return;
            }

            // Wait for a completion message
            _logger.Info("Ready at {0}. Waiting for Shutdown", DateTime.Now);

            // Wait for the system to shut down, but perform a heartbeat every minute.
            using (Observable.Interval(TimeSpan.FromMinutes(1)).Subscribe(t => _logger.Info("Heartbeat tick. Host is still running.")))
            {
                _subject.Wait();
            }

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
            try
            {
                _logger.Debug("Executing Job '{0}'", name);

                try
                {
                    job.RunOnce();
                }
                catch (Exception ex)
                {
                    _logger.ErrorException(String.Format("Error Executing Job '{0}': {1}", name, ex.Message), ex);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException(String.Format("Infrastructure Error Executing Job '{0}': {1}", name, ex.Message), ex);
            }
        }
    }
}
