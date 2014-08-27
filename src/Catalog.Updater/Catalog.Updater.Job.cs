using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Common;

namespace Catalog.Updater
{
    internal class Job
    {
        public Job() { }

        public bool Init(string[] args)
        {
            var jobName = this.GetType().ToString();
            args = Configuration.GetJobArgs(args, jobName);

            if(args.Length == 0)
            {
                return false;
            }

            return true;
        }

        public void Run()
        {
            Console.WriteLine("Running...");
        }

        public void ShowHelp()
        {
            Console.WriteLine("No help message available...");
        }
    }
}
