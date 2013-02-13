using NuGetGallery.Operations.Worker;

namespace NuGetGallery.Operations.Tools.Tasks
{
    [Command("listworkerjobs", "Lists the available job in the worker", AltName="lwj")]
    public class ListWorkerJobs : OpsTask
    {
        public override void ExecuteCommand()
        {
            foreach (string job in WorkerRole.GetJobList())
            {
                Log.Info("* " + job);
            }
        }
    }
}
