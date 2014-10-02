using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tests.Logger
{
    internal class Job : JobBase
    {
        private const string ScenarioArgumentName = "scenario";
        private const string HelpMessage = "Please provide 1 for successful job, 2 for failed job, 3 for crashed job and 4 for successful job with heavy logging";

        private int? JobScenario { get; set; }
        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            JobScenario = JobConfigManager.TryGetIntArgument(jobArgsDictionary, ScenarioArgumentName);
            if(JobScenario == null)
            {
                throw new ArgumentException("Argument '"+ ScenarioArgumentName +"' is mandatory." + HelpMessage);
            }

            return true;
        }

        public async override Task<bool> Run()
        {
            switch(JobScenario)
            {
                case 1:
                    return true;
                    
                case 2:
                    return false;

                case 3:
                    throw new Exception("Job crashed test");

                case 4:
                    for(int i = 0; i < 200; i++)
                    {
                        Trace.WriteLine("Messge number : " + i);
                    }
                    return true;

                default:
                    throw new ArgumentException("Unknown scenario. " + HelpMessage);
            }
        }
    }
}
