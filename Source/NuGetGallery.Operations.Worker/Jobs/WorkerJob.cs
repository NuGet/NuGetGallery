using System;
using NLog;

namespace NuGetGallery.Operations.Worker.Jobs
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
            Logger = LogManager.GetLogger(GetType().Name);

            StatusMessage = string.Empty;
        }

        public virtual void Initialize(Settings settings)
        {
            Settings = settings;
        }

        public abstract void RunOnce();
        public virtual void OnStart() { }
        public virtual void OnStop() { }
    }
}
