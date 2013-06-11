using System;
using System.Threading;

namespace NuGetGallery.Worker.Jobs
{
    //[Export(typeof(WorkerJob))]
    public class DiagnosticsCollectionJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(4);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromHours(1);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Running Diagnostics Collection on Thread {0}", Thread.CurrentThread.ManagedThreadId);
            Thread.Sleep(2000);
        }
    }
}
