// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using NLog;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Backend.Jobs
{
    public abstract class WorkerJob
    {
        protected Logger Logger { get; private set; }

        public virtual TimeSpan Period { get { return TimeSpan.FromDays(1); } }
        public virtual TimeSpan Offset { get { return TimeSpan.Zero; } }

        protected Settings Settings { get; private set; }

        public string StatusMessage { get; protected set; }

        protected WorkerJob()
        {
            StatusMessage = string.Empty;

            Logger = LogManager.GetLogger("Job." + GetType().Name);
            Logger.Info("---- {0} STARTING ----", GetType().Name);
        }

        public virtual void Initialize(Settings settings)
        {
            Settings = settings;
        }

        public void ExecuteTask(OpsTask task)
        {
            Logger.Info("Starting Execution of {0}", task.GetType().Name);

            task.Log = Logger;

            bool completed = false;
            IAsyncCompletionTask completion = task as IAsyncCompletionTask;
            
            try
            {
                task.Execute();
            }
            catch (Exception ex)
            {
                Logger.Error("Execution of {0} failed: {1}", task.GetType().Name, ex.ToString());
                return;
            }

            if (completion != null)
            {
                DateTime startUtc = DateTime.UtcNow;
                while (DateTime.UtcNow - startUtc < completion.MaximumPollingLength && !completed)
                {
                    try
                    {
                        completed = completion.PollForCompletion();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Polling for completion of {0} failed: {1}", task.GetType().Name, ex.ToString());
                        return;
                    }
                }
            }
            else
            {
                completed = true;
            }

            if (!completed)
            {
                // If we're here, it means we hit the max poll length without recieving a success response
                Logger.Error("Asynchronous Execution of {0} failed!");
            }
            else
            {
                Logger.Info("Completed Execution of {0}", task.GetType().Name);
            }
        }

        public abstract void RunOnce();
    }
}
