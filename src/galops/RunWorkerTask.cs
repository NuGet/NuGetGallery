using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Targets;
using NuGetGallery.Backend;

namespace NuGetGallery.Operations.Tools.Tasks
{
    [Command("runworker", "Runs the Operations Worker", AltName="rw")]
    public class RunWorkerTask : OpsTask
    {
        [Option("Provide this parameter to run a single job, once.", AltName="j")]
        public string JobName { get; set; }

        [Option("Provide this parameter with a single job to run continuously.", AltName = "con")]
        public bool Continuous { get; set; }

        [Option("'key=value' pairs to use as override settings for the runner.", AltName = "s")]
        public IList<string> Setting { get; set; }

        public RunWorkerTask()
        {
            Setting = new List<string>();
        }

        public override void ExecuteCommand()
        {
            //TODO: Remove or fix the Settings. They currently don't wire up to anything.
            //TODO: If we decide to fix this. The model here at the Job level appears slightly different to the Task
            //TODO: specifically the Task has the arg-parsing framework do the split on arg1=val1;arg2=val2 

            // Extract and parse the settings
            IDictionary<string, string> overrideSettings = new Dictionary<string, string>();
            if (Setting != null)
            {
                bool cont = true;
                foreach (string setting in Setting)
                {
                    string[] splitted = setting.Split('=');
                    if (splitted.Length != 2)
                    {
                        Log.Error("Invalid Setting: {0}", setting);
                        cont = false;
                    }
                }
                if (!cont)
                {
                    return;
                }
            }

            if (WhatIf)
            {
                overrideSettings["WhatIf"] = "true";
            }

            // Run the worker role
            WorkerRole.Execute(JobName, Continuous, overrideSettings);
        }
    }
}
